using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceInkWin.Services;

namespace VoiceInkWin.ViewModels;

public partial class AdvancedSettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly HistoryService _historyService;

    [ObservableProperty] private ObservableCollection<string> _audioDevices = new();
    [ObservableProperty] private string _selectedAudioDevice = "";
    [ObservableProperty] private float _silenceThreshold;
    [ObservableProperty] private float _maxRecordingDuration;

    public AdvancedSettingsViewModel(SettingsService settingsService, HistoryService historyService)
    {
        _settingsService = settingsService;
        _historyService = historyService;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Settings;
        SilenceThreshold = s.SilenceThreshold;
        MaxRecordingDuration = s.MaxRecordingDuration;
        SelectedAudioDevice = s.AudioInputDeviceId;

        RefreshAudioDevices();
    }

    [RelayCommand]
    public void RefreshAudioDevices()
    {
        var devices = AudioCaptureService.GetInputDevices();
        AudioDevices = new ObservableCollection<string>(devices.Select(d => d.Name));
        if (!AudioDevices.Contains(SelectedAudioDevice) && AudioDevices.Count > 0)
            SelectedAudioDevice = AudioDevices[0];
    }

    partial void OnSelectedAudioDeviceChanged(string value)
    {
        _settingsService.Settings.AudioInputDeviceId = value;
    }

    partial void OnSilenceThresholdChanged(float value)
    {
        _settingsService.Settings.SilenceThreshold = value;
    }

    partial void OnMaxRecordingDurationChanged(float value)
    {
        _settingsService.Settings.MaxRecordingDuration = value;
    }

    [RelayCommand]
    public void ExportHistory()
    {
        try
        {
            var text = _historyService.ExportToText();
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "VoiceInk_History.txt");
            File.WriteAllText(path, text);
        }
        catch { }
    }

    [RelayCommand]
    public void OpenAppDataFolder()
    {
        try
        {
            Process.Start("explorer.exe", SettingsService.GetAppDataFolder());
        }
        catch { }
    }

    public void Save()
    {
        _settingsService.Save();
    }
}
