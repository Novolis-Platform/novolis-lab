using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CursorRemote.Desktop.Windows;

internal static class WindowsInput
{
    private const uint InputKeyboard = 1;
    private const uint InputMouse = 0;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint MouseRightDown = 0x0008;
    private const uint MouseRightUp = 0x0010;
    private const uint MouseMiddleDown = 0x0020;
    private const uint MouseMiddleUp = 0x0040;
    private const uint KeyUp = 0x0002;
    private const uint Unicode = 0x0004;
    private const ushort VkControl = 0x11;
    private const ushort VkShift = 0x10;
    private const ushort VkAlt = 0x12;
    private const ushort VkWindows = 0x5B;

    private static readonly IReadOnlyDictionary<string, ushort> Keys =
        new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            ["Backspace"] = 0x08,
            ["Tab"] = 0x09,
            ["Enter"] = 0x0D,
            ["Escape"] = 0x1B,
            ["Space"] = 0x20,
            ["PageUp"] = 0x21,
            ["PageDown"] = 0x22,
            ["End"] = 0x23,
            ["Home"] = 0x24,
            ["Left"] = 0x25,
            ["Up"] = 0x26,
            ["Right"] = 0x27,
            ["Down"] = 0x28,
            ["Insert"] = 0x2D,
            ["Delete"] = 0x2E,
            ["F1"] = 0x70,
            ["F2"] = 0x71,
            ["F3"] = 0x72,
            ["F4"] = 0x73,
            ["F5"] = 0x74,
            ["F6"] = 0x75,
            ["F7"] = 0x76,
            ["F8"] = 0x77,
            ["F9"] = 0x78,
            ["F10"] = 0x79,
            ["F11"] = 0x7A,
            ["F12"] = 0x7B,
        };

    public static bool FocusCursor()
    {
        foreach (var process in Process.GetProcessesByName("Cursor"))
        {
            try
            {
                var handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero)
                    continue;

                // SW_SHOWMAXIMIZED — restore (9) unmaximizes and shrinks the capture.
                ShowWindow(handle, 3);
                SetForegroundWindow(handle);
                return true;
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }

    public static bool Click(
        double x,
        double y,
        string button,
        int clickCount)
    {
        var region = WindowsScreenCapture.CurrentRegion;
        var screenX = (int)Math.Round(region.OriginX + x);
        var screenY = (int)Math.Round(region.OriginY + y);
        SetCursorPos(screenX, screenY);

        var flags = button.ToLowerInvariant() switch
        {
            "right" => (MouseRightDown, MouseRightUp),
            "middle" => (MouseMiddleDown, MouseMiddleUp),
            _ => (MouseLeftDown, MouseLeftUp),
        };

        var count = Math.Clamp(clickCount, 1, 3);
        for (var i = 0; i < count; i++)
        {
            SendMouse(flags.Item1);
            SendMouse(flags.Item2);
        }

        return true;
    }

    public static bool TypeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        var inputs = new List<Input>(text.Length * 2);
        foreach (var character in text)
        {
            inputs.Add(UnicodeKey(character, keyUp: false));
            inputs.Add(UnicodeKey(character, keyUp: true));
        }

        return Send(inputs);
    }

    public static bool SendKey(string specification)
    {
        if (string.IsNullOrWhiteSpace(specification))
            return false;

        var parts = specification.Split(
            '+',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        var modifiers = new List<ushort>();
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var modifier = parts[i].ToLowerInvariant() switch
            {
                "ctrl" or "control" => VkControl,
                "shift" => VkShift,
                "alt" => VkAlt,
                "win" or "windows" => VkWindows,
                _ => (ushort)0,
            };
            if (modifier == 0)
                return false;
            modifiers.Add(modifier);
        }

        if (!TryResolveKey(parts[^1], out var key))
            return false;

        var inputs = new List<Input>((modifiers.Count + 1) * 2);
        foreach (var modifier in modifiers)
            inputs.Add(VirtualKey(modifier, keyUp: false));
        inputs.Add(VirtualKey(key, keyUp: false));
        inputs.Add(VirtualKey(key, keyUp: true));
        for (var i = modifiers.Count - 1; i >= 0; i--)
            inputs.Add(VirtualKey(modifiers[i], keyUp: true));

        return Send(inputs);
    }

    private static bool TryResolveKey(string value, out ushort key)
    {
        if (Keys.TryGetValue(value, out key))
            return true;

        if (value.Length == 1)
        {
            var character = char.ToUpperInvariant(value[0]);
            if (character is >= 'A' and <= 'Z')
            {
                key = character;
                return true;
            }

            if (character is >= '0' and <= '9')
            {
                key = character;
                return true;
            }
        }

        key = 0;
        return false;
    }

    private static void SendMouse(uint flags)
    {
        var input = new Input
        {
            Type = InputMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInput
                {
                    Flags = flags,
                },
            },
        };
        Send([input]);
    }

    private static Input UnicodeKey(char character, bool keyUp) =>
        new()
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    ScanCode = character,
                    Flags = Unicode | (keyUp ? KeyUp : 0),
                },
            },
        };

    private static Input VirtualKey(ushort key, bool keyUp) =>
        new()
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = key,
                    Flags = keyUp ? KeyUp : 0,
                },
            },
        };

    private static bool Send(IReadOnlyList<Input> inputs)
    {
        if (inputs.Count == 0)
            return true;

        var array = inputs.ToArray();
        return SendInput(
                   (uint)array.Length,
                   array,
                   Marshal.SizeOf<Input>())
               == array.Length;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint numberOfInputs,
        Input[] inputs,
        int sizeOfInput);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
}
