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

    private readonly SemaphoreSlim _recordingLock = new(1, 1);
    private volatile bool _isProcessing;

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

        // Wire audio events — BeginInvoke to avoid blocking the hook thread
        _audioCaptureService.AudioDataAvailable += OnAudioData;
        _audioCaptureService.SilenceTimeout += () => Application.Current?.Dispatcher.BeginInvoke(StopAndTranscribe);
        _audioCaptureService.MaxDurationReached += () => Application.Current?.Dispatcher.BeginInvoke(StopAndTranscribe);

        // Wire hotkey — BeginInvoke to avoid blocking the low-level keyboard hook
        _hotkeyService.ToggleRecording += () => Application.Current?.Dispatcher.BeginInvoke(OnToggleRecording);
        _hotkeyService.PttKeyDown += () => Application.Current?.Dispatcher.BeginInvoke(StartRecording);
        _hotkeyService.PttKeyUp += () => Application.Current?.Dispatcher.BeginInvoke(StopAndTranscribe);
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
        bool ptt = settings.RecordingMode == "pushToTalk";
        _hotkeyService.SetHotkey((uint)settings.HotkeyKeyCode, ptt);
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
        if (IsRecording || IsTranscribing || _isProcessing) return;

        if (!_transcriptionService.IsModelLoaded)
        {
            StatusText = "No model loaded - open Settings";
            return;
        }

        var settings = _settingsService.Settings;
        _audioCaptureService.Configure(settings.AudioInputDeviceId, settings.SilenceThreshold, settings.MaxRecordingDuration);
        // Disable silence detection in PTT mode — user controls stop by releasing the key
        _audioCaptureService.SilenceDetectionEnabled = settings.RecordingMode != "pushToTalk";

        try
        {
            _pasteService.SaveForegroundWindow();
            _audioCaptureService.StartRecording(settings.AudioInputDeviceId);
            IsRecording = true;
            CurrentState = AppState.Recording;
            StatusText = "Recording...";
            RecordingStarted?.Invoke();
            StateChanged?.Invoke();
            App.Log("Recording started");
        }
        catch (Exception ex)
        {
            StatusText = $"Mic error: {ex.Message}";
            App.Log($"Mic error: {ex}");
        }
    }

    public void StopAndTranscribe() => _ = StopAndTranscribeAsync();

    [RelayCommand]
    public async Task StopAndTranscribeAsync()
    {
        if (!IsRecording) return;

        // Non-blocking try-acquire to prevent double-fire
        if (!await _recordingLock.WaitAsync(0)) return;

        try
        {
            _isProcessing = true;

            var audioData = _audioCaptureService.StopRecording();
            IsRecording = false;
            IsTranscribing = true;
            CurrentState = AppState.Transcribing;
            StatusText = "Transcribing...";
            RecordingStopped?.Invoke();
            StateChanged?.Invoke();

            // Calculate audio RMS to check if mic is actually picking up sound
            double rmsSum = 0;
            float maxSample = 0;
            for (int i = 0; i < audioData.Length; i++)
            {
                rmsSum += audioData[i] * audioData[i];
                float abs = Math.Abs(audioData[i]);
                if (abs > maxSample) maxSample = abs;
            }
            float rms = audioData.Length > 0 ? (float)Math.Sqrt(rmsSum / audioData.Length) : 0;
            App.Log($"Audio buffer: {audioData.Length} samples ({audioData.Length / 16000.0:F2}s), RMS={rms:F6}, Peak={maxSample:F6}");

            if (audioData.Length < 1600) // Less than 0.1s at 16kHz
            {
                App.Log("Too short, skipping transcription");
                StatusText = "Too short";
                return;
            }

            if (rms < 0.0003f)
            {
                App.Log($"Audio too quiet (RMS={rms:F6}), skipping transcription");
                StatusText = "No speech detected";
                return;
            }

            var settings = _settingsService.Settings;

            App.Log("Starting transcription...");
            var rawText = await _transcriptionService.TranscribeAsync(
                audioData, settings.TranscriptionLanguage,
                settings.TranslateToEnglish, settings.VocabularyHints);
            App.Log($"Transcription result: \"{rawText}\"");

            if (string.IsNullOrWhiteSpace(rawText))
            {
                App.Log("No speech detected, skipping");
                StatusText = "No speech detected";
                return;
            }

            // AI post-processing
            var mode = _modeManager.GetMode(SelectedModeName) ?? _modeManager.AllModes[0];
            App.Log($"AI processing with mode: {mode.Name}");
            var processedText = await _aiPostProcessor.ProcessAsync(rawText, mode);
            App.Log($"Processed text: \"{processedText}\"");

            // Paste into active app
            App.Log("Pasting text...");
            await _pasteService.PasteTextAsync(processedText);

            // Add to history
            _historyService.AddEntry(processedText, mode.Name);
            OnPropertyChanged(nameof(HistoryEntries));

            // Play sound
            if (settings.PlaySoundOnComplete)
            {
                SystemSounds.Asterisk.Play();
            }

            App.Log("Done");
            StatusText = "Done";
        }
        catch (Exception ex)
        {
            App.Log($"Transcription error: {ex}");
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsTranscribing = false;
            CurrentState = AppState.Idle;
            _isProcessing = false;
            _recordingLock.Release();
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
