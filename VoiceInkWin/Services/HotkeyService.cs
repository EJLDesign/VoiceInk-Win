using System.Runtime.InteropServices;
using VoiceInkWin.Interop;

namespace VoiceInkWin.Services;

public class HotkeyService : IDisposable
{
    private NativeMethods.LowLevelKeyboardProc? _keyboardProc;
    private IntPtr _hookHandle;
    private bool _disposed;

    // Hotkey config
    private uint _hotkeyVk;
    private bool _isPushToTalk;

    // State tracking
    private bool _pttActive;
    private bool _hotkeyDown; // tracks if the hotkey is currently held (for modifier-only keys)

    public event Action? ToggleRecording;
    public event Action? PttKeyDown;
    public event Action? PttKeyUp;

    public void Start()
    {
        _keyboardProc = LowLevelKeyboardCallback;
        var moduleHandle = NativeMethods.GetModuleHandle(null);
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
    }

    public void SetHotkey(uint vkCode, bool pushToTalk)
    {
        _hotkeyVk = vkCode;
        _isPushToTalk = pushToTalk;
        _pttActive = false;
        _hotkeyDown = false;
    }

    private IntPtr LowLevelKeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _hotkeyVk != 0)
        {
            var kbd = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();
            bool isDown = msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;
            bool isUp = msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP;

            if (kbd.vkCode == _hotkeyVk)
            {
                if (isDown && !_hotkeyDown)
                {
                    _hotkeyDown = true;

                    if (_isPushToTalk && !_pttActive)
                    {
                        _pttActive = true;
                        PttKeyDown?.Invoke();
                    }
                }
                else if (isUp && _hotkeyDown)
                {
                    _hotkeyDown = false;

                    if (_isPushToTalk && _pttActive)
                    {
                        _pttActive = false;
                        PttKeyUp?.Invoke();
                    }
                    else if (!_isPushToTalk)
                    {
                        ToggleRecording?.Invoke();
                    }
                }
            }
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
        _keyboardProc = null;
    }
}
