using System.Runtime.InteropServices;
using System.Text;
using VoiceInkWin.Interop;
using static VoiceInkWin.App;

namespace VoiceInkWin.Services;

public class PasteService
{
    private IntPtr _savedForegroundWindow;

    /// <summary>
    /// Call this when recording starts to remember which window to paste into.
    /// </summary>
    public void SaveForegroundWindow()
    {
        _savedForegroundWindow = NativeMethods.GetForegroundWindow();
        var sb = new StringBuilder(256);
        NativeMethods.GetWindowText(_savedForegroundWindow, sb, 256);
        Log($"Saved foreground window: \"{sb}\" (0x{_savedForegroundWindow:X})");
    }

    public async Task PasteTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Set text to clipboard using Win32 API with retry
        if (!SetClipboardText(text))
        {
            Log("Clipboard: all retries failed");
            return;
        }

        // Restore focus with AttachThreadInput for reliable cross-process focus
        if (_savedForegroundWindow != IntPtr.Zero)
        {
            uint targetThread = NativeMethods.GetWindowThreadProcessId(_savedForegroundWindow, out _);
            uint currentThread = NativeMethods.GetCurrentThreadId();
            bool attached = NativeMethods.AttachThreadInput(currentThread, targetThread, true);
            Log($"AttachThreadInput({currentThread} → {targetThread}): {attached}");
            bool focused = NativeMethods.SetForegroundWindow(_savedForegroundWindow);
            Log($"SetForegroundWindow(0x{_savedForegroundWindow:X}): {focused}");
            NativeMethods.AttachThreadInput(currentThread, targetThread, false);
            await Task.Delay(150);
        }

        // Force-release ALL modifiers unconditionally (don't check GetAsyncKeyState —
        // the input queue may have stale modifier state even after physical release)
        ReleaseAllModifiers();
        await Task.Delay(50);

        var hwnd = NativeMethods.GetForegroundWindow();
        var sb2 = new StringBuilder(256);
        NativeMethods.GetWindowText(hwnd, sb2, 256);
        Log($"Pasting into window: \"{sb2}\" (0x{hwnd:X})");

        // Simulate Ctrl+V with scan codes (many modern apps require wScan to be set)
        var inputs = new NativeMethods.INPUT[4];

        ushort ctrlScan = (ushort)NativeMethods.MapVirtualKey(NativeMethods.VK_CONTROL, NativeMethods.MAPVK_VK_TO_VSC);
        ushort vScan = (ushort)NativeMethods.MapVirtualKey(NativeMethods.VK_V, NativeMethods.MAPVK_VK_TO_VSC);

        // Ctrl down
        inputs[0].type = NativeMethods.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = NativeMethods.VK_CONTROL;
        inputs[0].u.ki.wScan = ctrlScan;

        // V down
        inputs[1].type = NativeMethods.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = NativeMethods.VK_V;
        inputs[1].u.ki.wScan = vScan;

        // V up
        inputs[2].type = NativeMethods.INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = NativeMethods.VK_V;
        inputs[2].u.ki.wScan = vScan;
        inputs[2].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        // Ctrl up
        inputs[3].type = NativeMethods.INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = NativeMethods.VK_CONTROL;
        inputs[3].u.ki.wScan = ctrlScan;
        inputs[3].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        uint sent = NativeMethods.SendInput(4, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        Log($"Paste Ctrl+V sent (SendInput returned {sent})");
        if (sent == 0)
            Log($"Paste SendInput error: {Marshal.GetLastWin32Error()}");

        // If SendInput returned 0, it was blocked (e.g. UIPI). Fall back to WM_PASTE.
        if (sent == 0 && hwnd != IntPtr.Zero)
        {
            Log("SendInput blocked, falling back to WM_PASTE");
            NativeMethods.SendMessage(hwnd, NativeMethods.WM_PASTE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static bool SetClipboardText(string text)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    NativeMethods.EmptyClipboard();

                    int byteCount = (text.Length + 1) * 2;
                    IntPtr hGlobal = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (UIntPtr)byteCount);
                    if (hGlobal == IntPtr.Zero) return false;

                    IntPtr locked = NativeMethods.GlobalLock(hGlobal);
                    if (locked == IntPtr.Zero) return false;

                    Marshal.Copy(text.ToCharArray(), 0, locked, text.Length);
                    Marshal.WriteInt16(locked, text.Length * 2, 0);
                    NativeMethods.GlobalUnlock(hGlobal);

                    NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, hGlobal);
                    Thread.Sleep(50); // Ensure clipboard data fully committed
                    Log($"Clipboard set on attempt {attempt + 1}");
                    return true;
                }
                finally
                {
                    NativeMethods.CloseClipboard();
                }
            }

            Thread.Sleep(15);
        }

        return false;
    }

    /// <summary>
    /// Unconditionally release ALL modifier keys with proper scan codes.
    /// We don't check GetAsyncKeyState because the input queue may have stale state.
    /// </summary>
    private static void ReleaseAllModifiers()
    {
        // (vk, isExtended) — right-side keys need KEYEVENTF_EXTENDEDKEY
        (ushort vk, bool extended)[] modifiers =
        [
            (NativeMethods.VK_MENU, false),
            (NativeMethods.VK_LMENU, false),
            (NativeMethods.VK_RMENU, true),
            (NativeMethods.VK_CONTROL, false),
            (NativeMethods.VK_LCONTROL, false),
            (NativeMethods.VK_RCONTROL, true),
            (NativeMethods.VK_SHIFT, false),
            (NativeMethods.VK_LSHIFT, false),
            (NativeMethods.VK_RSHIFT, true),
            (NativeMethods.VK_LWIN, false),
        ];

        var releases = new NativeMethods.INPUT[modifiers.Length];
        for (int i = 0; i < modifiers.Length; i++)
        {
            var (vk, extended) = modifiers[i];
            uint flags = NativeMethods.KEYEVENTF_KEYUP;
            if (extended)
                flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;

            releases[i] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                u = new NativeMethods.INPUTUNION
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = (ushort)NativeMethods.MapVirtualKey(vk, NativeMethods.MAPVK_VK_TO_VSC),
                        dwFlags = flags
                    }
                }
            };
        }

        uint relSent = NativeMethods.SendInput((uint)releases.Length, releases, Marshal.SizeOf<NativeMethods.INPUT>());
        Log($"Released all {releases.Length} modifier keys unconditionally (SendInput returned {relSent})");
        if (relSent == 0)
            Log($"ReleaseAllModifiers SendInput error: {Marshal.GetLastWin32Error()}");
    }
}
