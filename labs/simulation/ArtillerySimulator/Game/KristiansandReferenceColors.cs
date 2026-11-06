using System.Drawing;
using SkiaSharp;

namespace ArtillerySimulator.Game;

/// <summary>One-time bake of <c>kristiansand-reference.png</c> for table cell tinting.</summary>
internal static class KristiansandReferenceColors
{
    private static bool _loadAttempted;
    private static byte[]? _rgb;
    private static int _width;
    private static int _height;

    public static bool TrySample(float xMeters, float zMeters, out Color color)
    {
        color = default;
        if (!EnsureLoaded() || _rgb is null)
            return false;

        if (!TryReliefPixel(xMeters, zMeters, _width, _height, out var ix, out var iy))
            return false;

        var o = (iy * _width + ix) * 3;
        color = Color.FromArgb(255, _rgb[o], _rgb[o + 1], _rgb[o + 2]);
        return true;
    }

    private static bool EnsureLoaded()
    {
        if (_rgb is not null)
            return true;

        if (_loadAttempted)
            return false;

        _loadAttempted = true;
        var path = Path.Combine(AppContext.BaseDirectory, "map", "kristiansand-reference.png");
        if (!File.Exists(path))
            return false;

        try
        {
            using var bitmap = SKBitmap.Decode(path);
            if (bitmap is null)
                return false;

            _width = bitmap.Width;
            _height = bitmap.Height;
            _rgb = new byte[_width * _height * 3];
            var i = 0;
            for (var y = 0; y < _height; y++)
            {
                for (var x = 0; x < _width; x++)
                {
                    var c = bitmap.GetPixel(x, y);
                    _rgb[i++] = c.Red;
                    _rgb[i++] = c.Green;
                    _rgb[i++] = c.Blue;
                }
            }

            return true;
        }
        catch
        {
            _rgb = null;
            return false;
        }
    }

    private static bool TryReliefPixel(float xMeters, float zMeters, int width, int height, out int ix, out int iy)
    {
        if (ArtilleryMapStackGeoref.TryWorldToReliefPixel(xMeters, zMeters, out ix, out iy))
            return true;

        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        if (east <= 0f || north <= 0f)
        {
            ix = iy = 0;
            return false;
        }

        var u = xMeters / east;
        var v = zMeters / north;
        ix = (int)MathF.Round(Math.Clamp(u, 0f, 1f) * (width - 1));
        iy = (int)MathF.Round((1f - v) * (height - 1));
        return true;
    }
}
