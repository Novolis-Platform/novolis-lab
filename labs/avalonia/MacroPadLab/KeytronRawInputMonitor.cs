using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace MacroPadLab;

/// <summary>
/// Receives Windows Raw Input for the exact Keytron when the keyboard class
/// driver prevents a direct HID stream from being opened.
/// </summary>
internal sealed class KeytronRawInputMonitor : IDisposable
{
    private const uint RidInput = 0x10000003;
    private const uint RidiDeviceName = 0x20000007;
    private const uint RidevRemove = 0x00000001;
    private const uint RidevInputSink = 0x00000100;
    private const uint RidevDevNotify = 0x00002000;
    private const uint RimTypeKeyboard = 1;
    private const uint RimTypeHid = 2;
    private const uint WmInput = 0x00FF;
    private const ushort UsagePageGenericDesktop = 0x01;
    private const ushort UsageKeyboard = 0x06;
    private const ushort UsageMouse = 0x02;
    private const ushort UsagePageConsumer = 0x0C;
    private const ushort UsageConsumerControl = 0x01;

    private readonly IntPtr _windowHandle;
    private readonly string _deviceIdentity;
    private readonly Action<string> _onActivity;
    private readonly WndProc _wndProc;
    private readonly IntPtr _previousWndProc;
    private bool _disposed;

    private KeytronRawInputMonitor(
        IntPtr windowHandle,
        ushort vendorId,
        ushort productId,
        Action<string> onActivity)
    {
        _windowHandle = windowHandle;
        _deviceIdentity = $"vid_{vendorId:X4}&pid_{productId:X4}";
        _onActivity = onActivity;
        _wndProc = HandleWindowMessage;

        var registrations = CreateRegistrations(RidevInputSink | RidevDevNotify);
        if (!RegisterRawInputDevices(
                registrations,
                (uint)registrations.Length,
                (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "RegisterRawInputDevices failed.");
        }

        try
        {
            _previousWndProc = SetWindowLongPtr(
                _windowHandle,
                WindowLongIndex.WndProc,
                _wndProc);
            if (_previousWndProc == IntPtr.Zero)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "SetWindowLongPtr failed.");
            }
        }
        catch
        {
            RemoveRegistrations();
            throw;
        }
    }

    public static KeytronRawInputMonitor Start(
        IntPtr windowHandle,
        ushort vendorId,
        ushort productId,
        Action<string> onActivity)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Keytron Raw Input monitoring requires Windows.");
        }

        if (windowHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "The lab window does not have a native Windows handle yet.");
        }

        ArgumentNullException.ThrowIfNull(onActivity);
        return new KeytronRawInputMonitor(
            windowHandle,
            vendorId,
            productId,
            onActivity);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_previousWndProc != IntPtr.Zero)
        {
            SetWindowLongPtr(
                _windowHandle,
                WindowLongIndex.WndProc,
                _previousWndProc);
        }

        RemoveRegistrations();
        GC.SuppressFinalize(this);
    }

    private IntPtr HandleWindowMessage(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (!_disposed && message == WmInput)
        {
            try
            {
                ProcessInput(lParam);
            }
            catch (Exception exception)
            {
                _onActivity($"RAWINPUT failed: {exception.Message}");
            }
        }

        return CallWindowProc(
            _previousWndProc,
            windowHandle,
            message,
            wParam,
            lParam);
    }

    private void ProcessInput(IntPtr rawInputHandle)
    {
        var headerSize = (uint)(8 + IntPtr.Size * 2);
        uint byteCount = 0;
        var result = GetRawInputData(
            rawInputHandle,
            RidInput,
            null,
            ref byteCount,
            headerSize);
        if (result == uint.MaxValue || byteCount == 0)
        {
            return;
        }

        var buffer = new byte[checked((int)byteCount)];
        var requested = byteCount;
        result = GetRawInputData(
            rawInputHandle,
            RidInput,
            buffer,
            ref requested,
            headerSize);
        if (result == uint.MaxValue || requested < headerSize)
        {
            return;
        }

        var deviceHandle = ReadPointer(buffer, 8);
        var devicePath = ReadDevicePath(deviceHandle);
        if (string.IsNullOrWhiteSpace(devicePath)
            || devicePath.IndexOf(_deviceIdentity, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return;
        }

        var type = BitConverter.ToUInt32(buffer, 0);
        var dataOffset = checked((int)headerSize);
        var raw = Convert.ToHexString(buffer);
        switch (type)
        {
            case RimTypeKeyboard:
                ProcessKeyboard(buffer, dataOffset, devicePath, raw);
                break;
            case RimTypeHid:
                ProcessHid(buffer, dataOffset, devicePath, raw);
                break;
            default:
                _onActivity(
                    $"RAWINPUT type={type} device={devicePath} raw={raw}");
                break;
        }
    }

    private void ProcessKeyboard(
        byte[] buffer,
        int dataOffset,
        string devicePath,
        string raw)
    {
        if (buffer.Length < dataOffset + 16)
        {
            return;
        }

        var makeCode = BitConverter.ToUInt16(buffer, dataOffset);
        var flags = BitConverter.ToUInt16(buffer, dataOffset + 2);
        var virtualKey = BitConverter.ToUInt16(buffer, dataOffset + 6);
        var message = BitConverter.ToUInt32(buffer, dataOffset + 8);
        var state = (flags & 0x0001) == 0 ? "down" : "up";
        _onActivity(
            $"RAWINPUT keyboard {state} " +
            $"key={DescribeVirtualKey(virtualKey)} " +
            $"vk=0x{virtualKey:X2} scan=0x{makeCode:X2} " +
            $"message=0x{message:X4} device={devicePath} raw={raw}");
    }

    private void ProcessHid(
        byte[] buffer,
        int dataOffset,
        string devicePath,
        string raw)
    {
        if (buffer.Length < dataOffset + 8)
        {
            return;
        }

        var reportSize = BitConverter.ToUInt32(buffer, dataOffset);
        var reportCount = BitConverter.ToUInt32(buffer, dataOffset + 4);
        var payloadOffset = checked(dataOffset + 8);
        var payloadLength = Math.Min(
            checked((long)reportSize * reportCount),
            buffer.Length - payloadOffset);
        var payload = payloadLength <= 0
            ? string.Empty
            : Convert.ToHexString(
                buffer.AsSpan(payloadOffset, checked((int)payloadLength)));
        _onActivity(
            $"RAWINPUT hid reports={reportCount} reportSize={reportSize} " +
            $"device={devicePath} payload={payload} raw={raw}");
    }

    private string? ReadDevicePath(IntPtr deviceHandle)
    {
        if (deviceHandle == IntPtr.Zero)
        {
            return null;
        }

        uint characterCount = 0;
        var result = GetRawInputDeviceInfo(
            deviceHandle,
            RidiDeviceName,
            null,
            ref characterCount);
        if (result == uint.MaxValue || characterCount == 0)
        {
            return null;
        }

        var path = new StringBuilder(checked((int)characterCount + 1));
        result = GetRawInputDeviceInfo(
            deviceHandle,
            RidiDeviceName,
            path,
            ref characterCount);
        return result == uint.MaxValue
            ? null
            : path.ToString();
    }

    private static IntPtr ReadPointer(byte[] buffer, int offset)
    {
        return IntPtr.Size == 8
            ? new IntPtr(BitConverter.ToInt64(buffer, offset))
            : new IntPtr(BitConverter.ToInt32(buffer, offset));
    }

    private void RemoveRegistrations()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var registrations = CreateRegistrations(RidevRemove);
        RegisterRawInputDevices(
            registrations,
            (uint)registrations.Length,
            (uint)Marshal.SizeOf<RawInputDevice>());
    }

    private RawInputDevice[] CreateRegistrations(uint flags) =>
    [
        new()
        {
            UsagePage = UsagePageGenericDesktop,
            Usage = UsageKeyboard,
            Flags = flags,
            TargetWindow = flags == RidevRemove ? IntPtr.Zero : _windowHandle,
        },
        new()
        {
            UsagePage = UsagePageGenericDesktop,
            Usage = UsageMouse,
            Flags = flags,
            TargetWindow = flags == RidevRemove ? IntPtr.Zero : _windowHandle,
        },
        new()
        {
            UsagePage = UsagePageConsumer,
            Usage = UsageConsumerControl,
            Flags = flags,
            TargetWindow = flags == RidevRemove ? IntPtr.Zero : _windowHandle,
        },
    ];

    private static string DescribeVirtualKey(ushort virtualKey) =>
        virtualKey switch
        {
            >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
            >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x10 => "Shift",
            0x11 => "Ctrl",
            0x12 => "Alt",
            0x13 => "Pause",
            0x14 => "Caps Lock",
            0x1B => "Escape",
            0x20 => "Space",
            0x21 => "Page Up",
            0x22 => "Page Down",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left arrow",
            0x26 => "Up arrow",
            0x27 => "Right arrow",
            0x28 => "Down arrow",
            0x2C => "Print Screen",
            0x2D => "Insert",
            0x2E => "Delete",
            >= 0x70 and <= 0x87 => $"F{virtualKey - 0x6F}",
            0x90 => "Num Lock",
            0x91 => "Scroll Lock",
            0xA0 => "Left Shift",
            0xA1 => "Right Shift",
            0xA2 => "Left Ctrl",
            0xA3 => "Right Ctrl",
            0xA4 => "Left Alt",
            0xA5 => "Right Alt",
            0x5B => "Left GUI / Windows",
            0x5C => "Right GUI / Windows",
            _ => $"Virtual key 0x{virtualKey:X2}",
        };

    private static IntPtr SetWindowLongPtr(
        IntPtr windowHandle,
        WindowLongIndex index,
        WndProc wndProc)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(windowHandle, index, wndProc)
            : SetWindowLong32(windowHandle, index, wndProc);
    }

    private static IntPtr SetWindowLongPtr(
        IntPtr windowHandle,
        WindowLongIndex index,
        IntPtr wndProc)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(windowHandle, index, wndProc)
            : SetWindowLong32(windowHandle, index, wndProc);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        [In] RawInputDevice[] devices,
        uint deviceCount,
        uint deviceSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr rawInput,
        uint command,
        [Out] byte[]? data,
        ref uint dataSize,
        uint headerSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr device,
        uint command,
        StringBuilder? data,
        ref uint dataSize);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(
        IntPtr windowHandle,
        WindowLongIndex index,
        WndProc wndProc);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern IntPtr SetWindowLong32(
        IntPtr windowHandle,
        WindowLongIndex index,
        WndProc wndProc);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(
        IntPtr windowHandle,
        WindowLongIndex index,
        IntPtr wndProc);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern IntPtr SetWindowLong32(
        IntPtr windowHandle,
        WindowLongIndex index,
        IntPtr wndProc);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(
        IntPtr previousWndProc,
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr TargetWindow;
    }

    private enum WindowLongIndex : int
    {
        WndProc = -4,
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WndProc(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam);
}
