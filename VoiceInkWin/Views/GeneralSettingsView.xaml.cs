using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VoiceInkWin.Interop;
using VoiceInkWin.ViewModels;

namespace VoiceInkWin.Views;

public partial class GeneralSettingsView : UserControl
{
    public GeneralSettingsView()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not GeneralSettingsViewModel vm || !vm.IsCapturingHotkey)
            return;

        // Skip modifier-only presses
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System)
        {
            // If System key, check actual key
            if (e.Key == Key.System && e.SystemKey is not (Key.LeftAlt or Key.RightAlt))
            {
                // This is Alt + another key
                int vk = KeyInterop.VirtualKeyFromKey(e.SystemKey);
                int mods = GetCurrentModifiers();
                vm.OnHotkeyCaptured(vk, mods);
                e.Handled = true;
            }
            return;
        }

        int keyCode = KeyInterop.VirtualKeyFromKey(e.Key);
        int modifiers = GetCurrentModifiers();
        vm.OnHotkeyCaptured(keyCode, modifiers);
        e.Handled = true;
    }

    private static int GetCurrentModifiers()
    {
        int mods = 0;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            mods |= (int)NativeMethods.MOD_CONTROL;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            mods |= (int)NativeMethods.MOD_ALT;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            mods |= (int)NativeMethods.MOD_SHIFT;
        return mods;
    }
}
