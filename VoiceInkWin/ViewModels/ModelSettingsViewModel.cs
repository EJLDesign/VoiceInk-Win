using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceInkWin.Models;
using VoiceInkWin.Services;

namespace VoiceInkWin.ViewModels;

public partial class ModelSettingsViewModel : ObservableObject
{
    private readonly ModelManagerService _modelManager;
    private readonly SettingsService _settingsService;

    [ObservableProperty] private ObservableCollection<ModelInfo> _availableModels = new();
    [ObservableProperty] private string _selectedModelName = "";
    [ObservableProperty] private string _transcriptionLanguage = "en";
    [ObservableProperty] private bool _translateToEnglish;
    [ObservableProperty] private string _vocabularyHints = "";
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private double _downloadProgress;
    [ObservableProperty] private string _downloadStatus = "";

    private CancellationTokenSource? _downloadCts;

    public ModelSettingsViewModel(ModelManagerService modelManager, SettingsService settingsService)
    {
        _modelManager = modelManager;
        _settingsService = settingsService;

        _modelManager.DownloadProgressChanged += p =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() => DownloadProgress = p * 100);
        };
        _modelManager.StatusChanged += s =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() => DownloadStatus = s);
        };

        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Settings;
        SelectedModelName = s.SelectedModelName;
        TranscriptionLanguage = s.TranscriptionLanguage;
        TranslateToEnglish = s.TranslateToEnglish;
        VocabularyHints = s.VocabularyHints;
        RefreshModels();
    }

    [RelayCommand]
    public void RefreshModels()
    {
        var models = _modelManager.GetAvailableModels();
        AvailableModels = new ObservableCollection<ModelInfo>(models);
    }

    [RelayCommand]
    public async Task DownloadModel(ModelInfo model)
    {
        if (IsDownloading) return;

        IsDownloading = true;
        DownloadProgress = 0;
        _downloadCts = new CancellationTokenSource();

        try
        {
            await _modelManager.DownloadModelAsync(model, _downloadCts.Token);
            RefreshModels();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            DownloadStatus = $"Failed: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            _downloadCts = null;
        }
    }

    [RelayCommand]
    public void CancelDownload()
    {
        _downloadCts?.Cancel();
    }

    [RelayCommand]
    public void DeleteModel(ModelInfo model)
    {
        _modelManager.DeleteModel(model.FileName);
        RefreshModels();
    }

    partial void OnSelectedModelNameChanged(string value)
    {
        _settingsService.Settings.SelectedModelName = value;
    }

    partial void OnTranscriptionLanguageChanged(string value)
    {
        _settingsService.Settings.TranscriptionLanguage = value;
    }

    partial void OnTranslateToEnglishChanged(bool value)
    {
        _settingsService.Settings.TranslateToEnglish = value;
    }

    partial void OnVocabularyHintsChanged(string value)
    {
        _settingsService.Settings.VocabularyHints = value;
    }

    public void Save()
    {
        _settingsService.Save();
    }
}
