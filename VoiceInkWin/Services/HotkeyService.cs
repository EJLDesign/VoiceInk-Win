using System.Runtime.InteropServices;
using VoiceInkWin.Interop;

namespace VoiceInkWin.Services;

public class HotkeyService : IDisposable
{
    private readonly HotkeyInterop _interop = new();
    private NativeMethods.LowLevelKeyboardProc? _keyboardProc;
    private IntPtr _hookHandle;
    private bool _pttActive;
    private uint _pttKeyCode;
    private uint _pttModifiers;
    private bool _disposed;

    public event Action? ToggleRecording;
    public event Action? PttKeyDown;
    public event Action? PttKeyUp;

    public void Start()
    {
        _interop.Start();
        _interop.HotkeyPressed += () => ToggleRecording?.Invoke();
    }

    public bool RegisterToggleHotkey(int keyCode, int modifiers)
    {
        if (keyCode == 0) return false;
        return _interop.Register(1, (uint)modifiers, (uint)keyCode);
    }

    public void UnregisterToggleHotkey()
    {
        _interop.Unregister();
    }

    public void EnablePushToTalk(uint keyCode, uint modifiers)
    {
        DisablePushToTalk();
        _pttKeyCode = keyCode;
        _pttModifiers = modifiers;
        _pttActive = false;

        _keyboardProc = LowLevelKeyboardCallback;
        var moduleHandle = NativeMethods.GetModuleHandle(null);
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
    }

    public void DisablePushToTalk()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
        _keyboardProc = null;
        _pttActive = false;
    }

    private IntPtr LowLevelKeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kbd = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();

            if (kbd.vkCode == _pttKeyCode)
            {
                bool modifiersMatch = CheckModifiers(_pttModifiers);

                if ((msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN) && !_pttActive && modifiersMatch)
                {
                    _pttActive = true;
                    PttKeyDown?.Invoke();
                }
                else if ((msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP) && _pttActive)
                {
                    _pttActive = false;
                    PttKeyUp?.Invoke();
                }
            }
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static bool CheckModifiers(uint required)
    {
        if (required == 0) return true;

        bool ctrl = (required & NativeMethods.MOD_CONTROL) != 0;
        bool alt = (required & NativeMethods.MOD_ALT) != 0;
        bool shift = (required & NativeMethods.MOD_SHIFT) != 0;

        if (ctrl && NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) >= 0) return false;
        if (alt && NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) >= 0) return false;
        if (shift && NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) >= 0) return false;

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisablePushToTalk();
        _interop.Dispose();
    }
}
