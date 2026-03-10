using System.Windows.Controls;
using System.Windows.Input;

namespace VoiceInkWin.Views;

public partial class HotkeyRecorderControl : UserControl
{
    public HotkeyRecorderControl()
    {
        InitializeComponent();
    }

    private void Border_MouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
    }
}
