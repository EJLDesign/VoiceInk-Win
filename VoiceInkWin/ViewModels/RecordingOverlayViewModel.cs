using CommunityToolkit.Mvvm.ComponentModel;
using VoiceInkWin.Services;

namespace VoiceInkWin.ViewModels;

public partial class RecordingOverlayViewModel : ObservableObject
{
    private readonly AudioAnalysisService _audioAnalysis;

    [ObservableProperty] private float[] _frequencyBands = new float[24];
    [ObservableProperty] private bool _isVisible;

    public RecordingOverlayViewModel(AudioAnalysisService audioAnalysis)
    {
        _audioAnalysis = audioAnalysis;
    }

    public void UpdateBands()
    {
        FrequencyBands = _audioAnalysis.FrequencyBands;
    }
}
