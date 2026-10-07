using Novolis.Math.Geometry;

namespace AssetStudioLab;

/// <summary>Hex and named colors for the visual language.</summary>
public static class VisualColors
{
    public static bool TryParse(string text, out Rgba32 color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var t = text.Trim();
        if (t.StartsWith('#') && (t.Length == 7 || t.Length == 9)
            && byte.TryParse(t[1..3], System.Globalization.NumberStyles.HexNumber, null, out var r)
            && byte.TryParse(t[3..5], System.Globalization.NumberStyles.HexNumber, null, out var g)
            && byte.TryParse(t[5..7], System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            byte a = 255;
            if (t.Length == 9)
                byte.TryParse(t[7..9], System.Globalization.NumberStyles.HexNumber, null, out a);
            color = new Rgba32(r, g, b, a);
            return true;
        }

        color = t.ToLowerInvariant() switch
        {
            "red" => Rgba32.Red,
            "white" => Rgba32.White,
            "black" => Rgba32.Black,
            "crimson" => Rgba32.Crimson,
            _ => default,
        };
        return t.Equals("red", StringComparison.OrdinalIgnoreCase)
               || t.Equals("white", StringComparison.OrdinalIgnoreCase)
               || t.Equals("black", StringComparison.OrdinalIgnoreCase)
               || t.Equals("crimson", StringComparison.OrdinalIgnoreCase);
    }
}
