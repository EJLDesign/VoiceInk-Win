using System.Runtime.InteropServices;
using System.Text;
using VoiceInkWin.Interop;

namespace VoiceInkWin.Services;

public class CredentialService
{
    private const string TargetPrefix = "VoiceInk:";

    public void SaveApiKey(string provider, string apiKey)
    {
        string target = TargetPrefix + provider;
        byte[] blob = Encoding.Unicode.GetBytes(apiKey);
        IntPtr blobPtr = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobPtr, blob.Length);
            var cred = new NativeMethods.CREDENTIAL
            {
                Type = NativeMethods.CRED_TYPE_GENERIC,
                TargetName = target,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobPtr,
                Persist = NativeMethods.CRED_PERSIST_LOCAL_MACHINE,
                UserName = provider
            };
            NativeMethods.CredWrite(ref cred, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
        }
    }

    public string? GetApiKey(string provider)
    {
        string target = TargetPrefix + provider;
        if (!NativeMethods.CredRead(target, NativeMethods.CRED_TYPE_GENERIC, 0, out IntPtr credPtr))
            return null;

        try
        {
            var cred = Marshal.PtrToStructure<NativeMethods.CREDENTIAL>(credPtr);
            if (cred.CredentialBlob == IntPtr.Zero || cred.CredentialBlobSize == 0)
                return null;

            byte[] blob = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, blob, 0, blob.Length);
            return Encoding.Unicode.GetString(blob);
        }
        finally
        {
            NativeMethods.CredFree(credPtr);
        }
    }

    public void DeleteApiKey(string provider)
    {
        string target = TargetPrefix + provider;
        NativeMethods.CredDelete(target, NativeMethods.CRED_TYPE_GENERIC, 0);
    }
}
