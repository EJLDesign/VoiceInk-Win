using CommunityToolkit.Mvvm.ComponentModel;

namespace VoiceInkWin.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public GeneralSettingsViewModel General { get; }
    public ModelSettingsViewModel Model { get; }
    public AISettingsViewModel AI { get; }
    public AdvancedSettingsViewModel Advanced { get; }

    public SettingsViewModel(
        GeneralSettingsViewModel general,
        ModelSettingsViewModel model,
        AISettingsViewModel ai,
        AdvancedSettingsViewModel advanced)
    {
        General = general;
        Model = model;
        AI = ai;
        Advanced = advanced;
    }
}
