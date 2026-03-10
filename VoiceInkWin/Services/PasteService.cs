using System.Runtime.InteropServices;
using System.Windows;
using VoiceInkWin.Interop;

namespace VoiceInkWin.Services;

public class PasteService
{
    public async Task PasteTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Save current clipboard content
        IDataObject? savedClipboard = null;
        try
        {
            savedClipboard = Clipboard.GetDataObject();
        }
        catch { }

        // Set text to clipboard
        Clipboard.SetText(text, TextDataFormat.UnicodeText);

        // Small delay to let clipboard settle
        await Task.Delay(50);

        // Release any held modifier keys first to avoid Ctrl+Alt+V etc.
        ReleaseModifierKeys();
        await Task.Delay(30);

        // Simulate Ctrl+V
        var inputs = new NativeMethods.INPUT[4];

        // Ctrl down
        inputs[0].type = NativeMethods.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = NativeMethods.VK_CONTROL;

        // V down
        inputs[1].type = NativeMethods.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = NativeMethods.VK_V;

        // V up
        inputs[2].type = NativeMethods.INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = NativeMethods.VK_V;
        inputs[2].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        // Ctrl up
        inputs[3].type = NativeMethods.INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = NativeMethods.VK_CONTROL;
        inputs[3].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        NativeMethods.SendInput(4, inputs, Marshal.SizeOf<NativeMethods.INPUT>());

        // Restore clipboard after delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            try
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (savedClipboard != null)
                    {
                        Clipboard.SetDataObject(savedClipboard, true);
                    }
                });
            }
            catch { }
        });
    }

    private static void ReleaseModifierKeys()
    {
        var releases = new List<NativeMethods.INPUT>();

        void AddRelease(ushort vk)
        {
            if (NativeMethods.GetAsyncKeyState(vk) < 0)
            {
                releases.Add(new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    u = new NativeMethods.INPUTUNION
                    {
                        ki = new NativeMethods.KEYBDINPUT
                        {
                            wVk = vk,
                            dwFlags = NativeMethods.KEYEVENTF_KEYUP
                        }
                    }
                });
            }
        }

        AddRelease(NativeMethods.VK_CONTROL);
        AddRelease(NativeMethods.VK_SHIFT);
        AddRelease(NativeMethods.VK_MENU);
        AddRelease(NativeMethods.VK_LWIN);

        if (releases.Count > 0)
        {
            NativeMethods.SendInput((uint)releases.Count, releases.ToArray(), Marshal.SizeOf<NativeMethods.INPUT>());
        }
    }
}
