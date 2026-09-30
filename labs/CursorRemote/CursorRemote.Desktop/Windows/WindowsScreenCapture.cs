using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CursorRemote.Protocol;

namespace CursorRemote.Desktop.Windows;

internal static class WindowsScreenCapture
{
    private const uint PrintWindowRenderFullContent = 0x00000002;
    private const int DwmwaExtendedFrameBounds = 9;

    public static Rectangle VirtualBounds => SystemInformation.VirtualScreen;

    /// <summary>
    /// Capture region in virtual-screen coordinates. Updated on each Capture() / status resolve.
    /// Click mapping uses Origin + local frame coordinates.
    /// </summary>
    public static CaptureRegion CurrentRegion { get; set; } =
        new(VirtualBounds.Left, VirtualBounds.Top, VirtualBounds.Width, VirtualBounds.Height);

    public static CaptureRegion ResolveRegion()
    {
        if (TryGetCursorWindowBounds(out var window))
            return new CaptureRegion(window.Left, window.Top, window.Width, window.Height);

        var primary = Screen.PrimaryScreen?.Bounds ?? VirtualBounds;
        return new CaptureRegion(primary.Left, primary.Top, primary.Width, primary.Height);
    }

    public static RemoteScreenFrame Capture(int maxWidth = 3200, long jpegQuality = 85)
    {
        var region = ResolveRegion();
        CurrentRegion = region;

        using var bitmap = new Bitmap(
            region.Width,
            region.Height,
            PixelFormat.Format32bppPArgb);

        var capturedFromWindow = false;
        if (TryGetCursorWindowHandle(out var hwnd)
            && IsSameRegion(region, hwnd))
        {
            capturedFromWindow = TryPrintWindow(hwnd, bitmap);
        }

        if (!capturedFromWindow)
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(
                region.OriginX,
                region.OriginY,
                0,
                0,
                new Size(region.Width, region.Height),
                CopyPixelOperation.SourceCopy);
        }

        Bitmap output = bitmap;
        var ownsOutput = false;
        if (maxWidth > 0 && bitmap.Width > maxWidth)
        {
            var height = (int)Math.Round(bitmap.Height * (double)maxWidth / bitmap.Width);
            output = new Bitmap(maxWidth, height, PixelFormat.Format32bppPArgb);
            ownsOutput = true;
            using var graphics = Graphics.FromImage(output);
            graphics.CompositingQuality =
                System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            graphics.InterpolationMode =
                System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode =
                System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            graphics.PixelOffsetMode =
                System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            graphics.DrawImage(bitmap, new Rectangle(0, 0, output.Width, output.Height));
        }

        try
        {
            using var stream = new MemoryStream();
            SaveJpeg(output, stream, jpegQuality);
            // Width/Height are logical capture pixels for click mapping, not image bytes size.
            return new RemoteScreenFrame(
                stream.ToArray(),
                region.Width,
                region.Height,
                region.OriginX,
                region.OriginY);
        }
        finally
        {
            if (ownsOutput)
                output.Dispose();
        }
    }

    private static void SaveJpeg(Bitmap bitmap, Stream stream, long quality)
    {
        var encoder = ImageCodecInfo.GetImageEncoders()
            .First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, Math.Clamp(quality, 40, 95));
        bitmap.Save(stream, encoder, parameters);
    }

    private static bool IsSameRegion(CaptureRegion region, IntPtr hwnd)
    {
        if (!TryGetWindowBounds(hwnd, out var bounds))
            return false;
        return bounds.Left == region.OriginX
               && bounds.Top == region.OriginY
               && bounds.Width == region.Width
               && bounds.Height == region.Height;
    }

    private static bool TryPrintWindow(IntPtr hwnd, Bitmap bitmap)
    {
        using var graphics = Graphics.FromImage(bitmap);
        var hdc = graphics.GetHdc();
        try
        {
            return PrintWindow(hwnd, hdc, PrintWindowRenderFullContent);
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }
    }

    private static bool TryGetCursorWindowBounds(out Rectangle bounds)
    {
        bounds = default;
        if (!TryGetCursorWindowHandle(out var hwnd))
            return false;
        return TryGetWindowBounds(hwnd, out bounds);
    }

    private static bool TryGetCursorWindowHandle(out IntPtr hwnd)
    {
        hwnd = IntPtr.Zero;
        foreach (var process in Process.GetProcessesByName("Cursor"))
        {
            try
            {
                var handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero || !IsWindowVisible(handle))
                    continue;
                hwnd = handle;
                return true;
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }

    private static bool TryGetWindowBounds(IntPtr hwnd, out Rectangle bounds)
    {
        bounds = default;
        if (DwmGetWindowAttribute(
                hwnd,
                DwmwaExtendedFrameBounds,
                out var rect,
                Marshal.SizeOf<NativeRect>()) == 0)
        {
            bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            return bounds.Width > 0 && bounds.Height > 0;
        }

        if (!GetWindowRect(hwnd, out rect))
            return false;

        bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        out NativeRect pvAttribute,
        int cbAttribute);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

internal readonly record struct CaptureRegion(
    int OriginX,
    int OriginY,
    int Width,
    int Height);
