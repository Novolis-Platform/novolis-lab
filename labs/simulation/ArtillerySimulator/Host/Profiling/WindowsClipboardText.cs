using System.Runtime.InteropServices;
using System.Text;

namespace ArtillerySimulator;

/// <summary>STA clipboard write for Silk hosts (no console, no WinForms dependency).</summary>
internal static class WindowsClipboardText
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;

    public static bool TrySet(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        if (!OperatingSystem.IsWindows())
            return false;

        var ok = false;
        var thread = new Thread(() =>
        {
            ok = SetOnSta(text);
        })
        {
            IsBackground = true,
            Name = "ArtilleryClipboard",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(millisecondsTimeout: 2_000);
        return ok;
    }

    private static bool SetOnSta(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
            return false;

        try
        {
            EmptyClipboard();
            var bytes = Encoding.Unicode.GetBytes(text + '\0');
            var hGlobal = GlobalAlloc(GmemMoveable, (UIntPtr)(uint)bytes.Length);
            if (hGlobal == IntPtr.Zero)
                return false;

            var target = GlobalLock(hGlobal);
            if (target == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                return false;
            }

            try
            {
                Marshal.Copy(bytes, 0, target, bytes.Length);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            if (SetClipboardData(CfUnicodeText, hGlobal) == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                return false;
            }

            // Ownership transferred to the clipboard.
            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
