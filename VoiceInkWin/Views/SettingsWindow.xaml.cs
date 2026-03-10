using System.Windows;
using VoiceInkWin.ViewModels;

namespace VoiceInkWin.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public bool Saved { get; private set; }

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;

        GeneralTab.DataContext = viewModel.General;
        ModelTab.DataContext = viewModel.Model;
        AITab.DataContext = viewModel.AI;
        AdvancedTab.DataContext = viewModel.Advanced;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.General.Save();
        _viewModel.Model.Save();
        _viewModel.AI.Save();
        _viewModel.Advanced.Save();
        Saved = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
