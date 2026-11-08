using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>Loads the title-bar icon from shipped PNG/JPG assets.</summary>
internal static class WindowIconLoader
{
    public static void TryApply(SilkFrame frame)
    {
        foreach (var relative in new[] { "assets/k9-dog-icon.png", "assets/k9-dog-icon.jpg" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, relative);
            if (!File.Exists(path))
                continue;

            try
            {
                using var bmp = SkiaSharp.SKBitmap.Decode(path);
                if (bmp is null)
                    continue;

                var w = bmp.Width;
                var h = bmp.Height;
                var bytes = new byte[checked(w * h * 4)];
                for (var y = 0; y < h; y++)
                {
                    for (var x = 0; x < w; x++)
                    {
                        var c = bmp.GetPixel(x, y);
                        var o = (y * w + x) * 4;
                        bytes[o] = c.Red;
                        bytes[o + 1] = c.Green;
                        bytes[o + 2] = c.Blue;
                        bytes[o + 3] = c.Alpha;
                    }
                }

                frame.SetWindowIcon(w, h, bytes);
                return;
            }
            catch
            {
                // Keep platform default icon.
            }
        }
    }
}
