using System.Media;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceInkWin.Models;
using VoiceInkWin.Services;

namespace VoiceInkWin.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly AudioCaptureService _audioCaptureService;
    private readonly AudioAnalysisService _audioAnalysisService;
    private readonly TranscriptionService _transcriptionService;
    private readonly AIPostProcessorService _aiPostProcessor;
    private readonly HotkeyService _hotkeyService;
    private readonly PasteService _pasteService;
    private readonly ModelManagerService _modelManager;
    private readonly ModeManager _modeManager;
    private readonly HistoryService _historyService;
    private readonly SettingsService _settingsService;

    [ObservableProperty] private string _statusText = "Idle";
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private bool _isTranscribing;
    [ObservableProperty] private string _selectedModeName = "Raw";

    public enum AppState { Idle, Recording, Transcribing }

    [ObservableProperty] private AppState _currentState = AppState.Idle;

    public IReadOnlyList<Mode> AllModes => _modeManager.AllModes;
    public IReadOnlyList<HistoryEntry> HistoryEntries => _historyService.Entries;

    public event Action<float[]>? AudioDataReceived;
    public event Action? RecordingStarted;
    public event Action? RecordingStopped;
    public event Action? StateChanged;

    public MainViewModel(
        AudioCaptureService audioCaptureService,
        AudioAnalysisService audioAnalysisService,
        TranscriptionService transcriptionService,
        AIPostProcessorService aiPostProcessor,
        HotkeyService hotkeyService,
        PasteService pasteService,
        ModelManagerService modelManager,
        ModeManager modeManager,
        HistoryService historyService,
        SettingsService settingsService)
    {
        _audioCaptureService = audioCaptureService;
        _audioAnalysisService = audioAnalysisService;
        _transcriptionService = transcriptionService;
        _aiPostProcessor = aiPostProcessor;
        _hotkeyService = hotkeyService;
        _pasteService = pasteService;
        _modelManager = modelManager;
        _modeManager = modeManager;
        _historyService = historyService;
        _settingsService = settingsService;

        _selectedModeName = settingsService.Settings.SelectedModeName;

        // Wire audio events
        _audioCaptureService.AudioDataAvailable += OnAudioData;
        _audioCaptureService.SilenceTimeout += () => Application.Current?.Dispatcher.Invoke(StopAndTranscribe);
        _audioCaptureService.MaxDurationReached += () => Application.Current?.Dispatcher.Invoke(StopAndTranscribe);

        // Wire hotkey
        _hotkeyService.ToggleRecording += () => Application.Current?.Dispatcher.Invoke(OnToggleRecording);
        _hotkeyService.PttKeyDown += () => Application.Current?.Dispatcher.Invoke(StartRecording);
        _hotkeyService.PttKeyUp += () => Application.Current?.Dispatcher.Invoke(StopAndTranscribe);
    }

    public void Initialize()
    {
        _modeManager.Load();
        _historyService.Load();

        var settings = _settingsService.Settings;

        // Configure audio
        _audioCaptureService.Configure(settings.AudioInputDeviceId, settings.SilenceThreshold, settings.MaxRecordingDuration);

        // Load model
        TryLoadModel();

        // Start hotkey service
        _hotkeyService.Start();
        RegisterHotkeys();
    }

    public void RegisterHotkeys()
    {
        var settings = _settingsService.Settings;

        if (settings.RecordingMode == "toggle")
        {
            _hotkeyService.DisablePushToTalk();
            _hotkeyService.RegisterToggleHotkey(settings.HotkeyKeyCode, settings.HotkeyModifiers);
        }
        else
        {
            _hotkeyService.UnregisterToggleHotkey();
            _hotkeyService.EnablePushToTalk((uint)settings.HotkeyKeyCode, (uint)settings.HotkeyModifiers);
        }
    }

    private void TryLoadModel()
    {
        var modelPath = _modelManager.GetModelPath(_settingsService.Settings.SelectedModelName);
        if (modelPath != null)
        {
            try
            {
                _transcriptionService.LoadModel(modelPath);
                StatusText = "Ready";
            }
            catch (Exception ex)
            {
                StatusText = $"Model load failed: {ex.Message}";
            }
        }
        else
        {
            StatusText = "No model - open Settings to download";
        }
    }

    private void OnToggleRecording()
    {
        if (IsRecording)
            StopAndTranscribe();
        else
            StartRecording();
    }

    [RelayCommand]
    public void StartRecording()
    {
        if (IsRecording || IsTranscribing) return;

        if (!_transcriptionService.IsModelLoaded)
        {
            StatusText = "No model loaded - open Settings";
            return;
        }

        var settings = _settingsService.Settings;
        _audioCaptureService.Configure(settings.AudioInputDeviceId, settings.SilenceThreshold, settings.MaxRecordingDuration);

        try
        {
            _audioCaptureService.StartRecording(settings.AudioInputDeviceId);
            IsRecording = true;
            CurrentState = AppState.Recording;
            StatusText = "Recording...";
            RecordingStarted?.Invoke();
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            StatusText = $"Mic error: {ex.Message}";
        }
    }

    public void StopAndTranscribe() => _ = StopAndTranscribeAsync();

    [RelayCommand]
    public async Task StopAndTranscribeAsync()
    {
        if (!IsRecording) return;

        var audioData = _audioCaptureService.StopRecording();
        IsRecording = false;
        IsTranscribing = true;
        CurrentState = AppState.Transcribing;
        StatusText = "Transcribing...";
        RecordingStopped?.Invoke();
        StateChanged?.Invoke();

        try
        {
            if (audioData.Length < 1600) // Less than 0.1s at 16kHz
            {
                StatusText = "Too short";
                return;
            }

            var settings = _settingsService.Settings;
            var rawText = await _transcriptionService.TranscribeAsync(
                audioData, settings.TranscriptionLanguage,
                settings.TranslateToEnglish, settings.VocabularyHints);

            if (string.IsNullOrWhiteSpace(rawText))
            {
                StatusText = "No speech detected";
                return;
            }

            // AI post-processing
            var mode = _modeManager.GetMode(SelectedModeName) ?? _modeManager.AllModes[0];
            var processedText = await _aiPostProcessor.ProcessAsync(rawText, mode);

            // Paste into active app
            await _pasteService.PasteTextAsync(processedText);

            // Add to history
            _historyService.AddEntry(processedText, mode.Name);
            OnPropertyChanged(nameof(HistoryEntries));

            // Play sound
            if (settings.PlaySoundOnComplete)
            {
                SystemSounds.Asterisk.Play();
            }

            StatusText = "Done";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsTranscribing = false;
            CurrentState = AppState.Idle;
            StateChanged?.Invoke();
        }
    }

    [RelayCommand]
    public void SelectMode(string modeName)
    {
        SelectedModeName = modeName;
        _settingsService.Settings.SelectedModeName = modeName;
        _settingsService.Save();
    }

    [RelayCommand]
    public void CopyHistoryEntry(HistoryEntry entry)
    {
        try
        {
            Clipboard.SetText(entry.Text, TextDataFormat.UnicodeText);
            StatusText = "Copied to clipboard";
        }
        catch { }
    }

    public void ReloadSettings()
    {
        var settings = _settingsService.Settings;
        _audioCaptureService.Configure(settings.AudioInputDeviceId, settings.SilenceThreshold, settings.MaxRecordingDuration);
        TryLoadModel();
        RegisterHotkeys();
        _modeManager.Load();
        OnPropertyChanged(nameof(AllModes));
        SelectedModeName = settings.SelectedModeName;
    }

    private void OnAudioData(float[] samples)
    {
        _audioAnalysisService.Analyze(samples);
        AudioDataReceived?.Invoke(samples);
    }

    public void Dispose()
    {
        _audioCaptureService.Dispose();
        _transcriptionService.Dispose();
        _hotkeyService.Dispose();
    }
}
