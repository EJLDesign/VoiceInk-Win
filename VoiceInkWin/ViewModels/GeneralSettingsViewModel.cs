using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using VoiceInkWin.Services;

namespace VoiceInkWin.ViewModels;

public partial class GeneralSettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;

    [ObservableProperty] private bool _isToggleMode;
    [ObservableProperty] private bool _isPushToTalk;
    [ObservableProperty] private bool _launchAtLogin;
    [ObservableProperty] private bool _playSoundOnComplete;
    [ObservableProperty] private string _hotkeyDisplay = "Not set";
    [ObservableProperty] private bool _isCapturingHotkey;

    private int _capturedKeyCode;
    private int _capturedModifiers;

    public GeneralSettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Settings;
        IsToggleMode = s.RecordingMode == "toggle";
        IsPushToTalk = s.RecordingMode == "pushToTalk";
        LaunchAtLogin = s.LaunchAtLogin;
        PlaySoundOnComplete = s.PlaySoundOnComplete;
        _capturedKeyCode = s.HotkeyKeyCode;
        _capturedModifiers = s.HotkeyModifiers;
        UpdateHotkeyDisplay();
    }

    partial void OnIsToggleModeChanged(bool value)
    {
        if (value)
        {
            _settingsService.Settings.RecordingMode = "toggle";
            IsPushToTalk = false;
        }
    }

    partial void OnIsPushToTalkChanged(bool value)
    {
        if (value)
        {
            _settingsService.Settings.RecordingMode = "pushToTalk";
            IsToggleMode = false;
        }
    }

    partial void OnLaunchAtLoginChanged(bool value)
    {
        _settingsService.Settings.LaunchAtLogin = value;
        SetStartup(value);
    }

    partial void OnPlaySoundOnCompleteChanged(bool value)
    {
        _settingsService.Settings.PlaySoundOnComplete = value;
    }

    public void OnHotkeyCaptured(int keyCode, int modifiers)
    {
        _capturedKeyCode = keyCode;
        _capturedModifiers = modifiers;
        _settingsService.Settings.HotkeyKeyCode = keyCode;
        _settingsService.Settings.HotkeyModifiers = modifiers;
        IsCapturingHotkey = false;
        UpdateHotkeyDisplay();
    }

    [RelayCommand]
    public void StartCaptureHotkey()
    {
        IsCapturingHotkey = true;
        HotkeyDisplay = "Press a key combination...";
    }

    [RelayCommand]
    public void ClearHotkey()
    {
        _capturedKeyCode = 0;
        _capturedModifiers = 0;
        _settingsService.Settings.HotkeyKeyCode = 0;
        _settingsService.Settings.HotkeyModifiers = 0;
        HotkeyDisplay = "Not set";
    }

    private void UpdateHotkeyDisplay()
    {
        if (_capturedKeyCode == 0)
        {
            HotkeyDisplay = "Not set";
            return;
        }

        // Check if it's a specific left/right modifier key
        string? specificName = _capturedKeyCode switch
        {
            0xA0 => "Left Shift",
            0xA1 => "Right Shift",
            0xA2 => "Left Ctrl",
            0xA3 => "Right Ctrl",
            0xA4 => "Left Alt",
            0xA5 => "Right Alt",
            _ => null
        };

        if (specificName != null && _capturedModifiers == 0)
        {
            HotkeyDisplay = specificName;
            return;
        }

        var parts = new List<string>();
        if ((_capturedModifiers & 0x0002) != 0) parts.Add("Ctrl");
        if ((_capturedModifiers & 0x0001) != 0) parts.Add("Alt");
        if ((_capturedModifiers & 0x0004) != 0) parts.Add("Shift");
        if ((_capturedModifiers & 0x0008) != 0) parts.Add("Win");

        var key = KeyInterop.KeyFromVirtualKey(_capturedKeyCode);
        parts.Add(key.ToString());

        HotkeyDisplay = string.Join(" + ", parts);
    }

    private static void SetStartup(bool enable)
    {
        const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string valueName = "VoiceInk";

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (exePath != null)
                    key.SetValue(valueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(valueName, false);
            }
        }
        catch { }
    }

    public void Save()
    {
        _settingsService.Save();
    }
}
