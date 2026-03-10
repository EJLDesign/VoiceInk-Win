using System.Windows.Controls;
using VoiceInkWin.ViewModels;

namespace VoiceInkWin.Views;

public partial class AISettingsView : UserControl
{
    public AISettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is AISettingsViewModel vm && !string.IsNullOrEmpty(vm.AnthropicApiKey))
            {
                ApiKeyBox.Password = vm.AnthropicApiKey;
            }
        };
    }

    private void ApiKeyBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AISettingsViewModel vm)
        {
            vm.AnthropicApiKey = ApiKeyBox.Password;
        }
    }
}
