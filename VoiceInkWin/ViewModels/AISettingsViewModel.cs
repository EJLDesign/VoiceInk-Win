using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceInkWin.Models;
using VoiceInkWin.Services;

namespace VoiceInkWin.ViewModels;

public partial class AISettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly CredentialService _credentialService;
    private readonly ModeManager _modeManager;

    [ObservableProperty] private bool _isNoneProvider;
    [ObservableProperty] private bool _isAnthropicProvider;
    [ObservableProperty] private bool _isOllamaProvider;
    [ObservableProperty] private string _anthropicApiKey = "";
    [ObservableProperty] private string _ollamaEndpoint = "http://localhost:11434";
    [ObservableProperty] private string _ollamaModelName = "llama3";
    [ObservableProperty] private ObservableCollection<Mode> _modes = new();

    // Custom mode editing
    [ObservableProperty] private string _newModeName = "";
    [ObservableProperty] private string _newModePrompt = "";

    public AISettingsViewModel(SettingsService settingsService, CredentialService credentialService, ModeManager modeManager)
    {
        _settingsService = settingsService;
        _credentialService = credentialService;
        _modeManager = modeManager;

        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Settings;
        IsNoneProvider = s.AiProvider == "none";
        IsAnthropicProvider = s.AiProvider == "anthropic";
        IsOllamaProvider = s.AiProvider == "ollama";
        OllamaEndpoint = s.OllamaEndpoint;
        OllamaModelName = s.OllamaModelName;

        AnthropicApiKey = _credentialService.GetApiKey("anthropic") ?? "";

        _modeManager.Load();
        Modes = new ObservableCollection<Mode>(_modeManager.AllModes);
    }

    partial void OnIsNoneProviderChanged(bool value)
    {
        if (value) _settingsService.Settings.AiProvider = "none";
    }

    partial void OnIsAnthropicProviderChanged(bool value)
    {
        if (value) _settingsService.Settings.AiProvider = "anthropic";
    }

    partial void OnIsOllamaProviderChanged(bool value)
    {
        if (value) _settingsService.Settings.AiProvider = "ollama";
    }

    partial void OnOllamaEndpointChanged(string value)
    {
        _settingsService.Settings.OllamaEndpoint = value;
    }

    partial void OnOllamaModelNameChanged(string value)
    {
        _settingsService.Settings.OllamaModelName = value;
    }

    [RelayCommand]
    public void SaveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(AnthropicApiKey))
            _credentialService.SaveApiKey("anthropic", AnthropicApiKey);
        else
            _credentialService.DeleteApiKey("anthropic");
    }

    [RelayCommand]
    public void AddCustomMode()
    {
        if (string.IsNullOrWhiteSpace(NewModeName) || string.IsNullOrWhiteSpace(NewModePrompt))
            return;

        _modeManager.AddCustomMode(NewModeName.Trim(), NewModePrompt.Trim());
        Modes = new ObservableCollection<Mode>(_modeManager.AllModes);
        NewModeName = "";
        NewModePrompt = "";
    }

    [RelayCommand]
    public void DeleteMode(Mode mode)
    {
        if (mode.IsBuiltIn) return;
        _modeManager.DeleteCustomMode(mode.Id);
        Modes = new ObservableCollection<Mode>(_modeManager.AllModes);
    }

    public void Save()
    {
        SaveApiKey();
        _settingsService.Save();
    }
}
