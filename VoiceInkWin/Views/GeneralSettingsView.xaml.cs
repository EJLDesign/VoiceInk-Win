using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VoiceInkWin.Interop;
using VoiceInkWin.ViewModels;

namespace VoiceInkWin.Views;

public partial class GeneralSettingsView : UserControl
{
    private bool _modifierOnlyPressed; // true if only a modifier is held, no other key yet
    private int _pendingVk;

    public GeneralSettingsView()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not GeneralSettingsViewModel vm || !vm.IsCapturingHotkey)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        int vk = KeyInterop.VirtualKeyFromKey(key);

        if (IsModifierKey(key))
        {
            // A modifier key is being held — remember it, wait for KeyUp or a non-modifier
            _modifierOnlyPressed = true;
            _pendingVk = MapToSpecificVk(key);
            e.Handled = true;
            return;
        }

        // A non-modifier key was pressed, so this is a combo (e.g. Ctrl+Space)
        _modifierOnlyPressed = false;
        int modifiers = GetCurrentModifiers();
        vm.OnHotkeyCaptured(vk, modifiers);
        e.Handled = true;
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (DataContext is not GeneralSettingsViewModel vm || !vm.IsCapturingHotkey)
            return;

        if (!_modifierOnlyPressed)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (IsModifierKey(key))
        {
            int releasedVk = MapToSpecificVk(key);
            if (releasedVk == _pendingVk)
            {
                // The modifier was pressed and released alone — capture it as the hotkey
                _modifierOnlyPressed = false;
                vm.OnHotkeyCaptured(_pendingVk, 0); // no separate modifiers, the key IS the hotkey
                e.Handled = true;
            }
        }
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin;

    /// <summary>
    /// Maps a WPF Key to the specific left/right VK code.
    /// </summary>
    private static int MapToSpecificVk(Key key) => key switch
    {
        Key.LeftCtrl => NativeMethods.VK_LCONTROL,
        Key.RightCtrl => NativeMethods.VK_RCONTROL,
        Key.LeftAlt => NativeMethods.VK_LMENU,
        Key.RightAlt => NativeMethods.VK_RMENU,
        Key.LeftShift => NativeMethods.VK_LSHIFT,
        Key.RightShift => NativeMethods.VK_RSHIFT,
        Key.LWin => NativeMethods.VK_LWIN,
        _ => KeyInterop.VirtualKeyFromKey(key)
    };

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
