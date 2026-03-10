using System.Runtime.InteropServices;

namespace VoiceInkWin.Interop;

public class HotkeyInterop : IDisposable
{
    private IntPtr _hwnd;
    private Thread? _messageThread;
    private bool _disposed;
    private readonly ManualResetEventSlim _ready = new();

    public event Action? HotkeyPressed;

    private int _hotkeyId;
    private uint _modifiers;
    private uint _vk;
    private bool _registered;

    public void Start()
    {
        _messageThread = new Thread(MessageLoop) { IsBackground = true, Name = "HotkeyMessageLoop" };
        _messageThread.SetApartmentState(ApartmentState.STA);
        _messageThread.Start();
        _ready.Wait(TimeSpan.FromSeconds(5));
    }

    private void MessageLoop()
    {
        _hwnd = NativeMethods.CreateWindowEx(
            0, "Static", "VoiceInkHotkeyWindow", 0,
            0, 0, 0, 0,
            NativeMethods.HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        _ready.Set();

        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0))
        {
            if (msg.message == NativeMethods.WM_HOTKEY)
            {
                HotkeyPressed?.Invoke();
            }
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }
    }

    public bool Register(int id, uint modifiers, uint vk)
    {
        Unregister();
        _hotkeyId = id;
        _modifiers = modifiers;
        _vk = vk;

        if (_hwnd == IntPtr.Zero) return false;

        _registered = NativeMethods.RegisterHotKey(_hwnd, id, modifiers | NativeMethods.MOD_NOREPEAT, vk);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hwnd, _hotkeyId);
            _registered = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unregister();
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.PostMessage(_hwnd, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }
        _ready.Dispose();
    }
}
