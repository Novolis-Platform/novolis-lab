using System.Globalization;
using SkiaSharp;

namespace ArtillerySimulator;

internal static class ShellOverlayTextWrap
{
    public static List<string> WrapLines(string body, float fontSize, float maxWidth)
    {
        var result = new List<string>(128);
        using var font = new SKFont(ShellOverlayTheme.Typeface, fontSize);
        foreach (var raw in body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (raw.Length == 0)
            {
                result.Add(string.Empty);
                continue;
            }

            if (font.MeasureText(raw) <= maxWidth)
            {
                result.Add(raw);
                continue;
            }

            var words = raw.Split(' ');
            var current = string.Empty;
            foreach (var word in words)
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (font.MeasureText(candidate) <= maxWidth)
                {
                    current = candidate;
                    continue;
                }

                if (current.Length > 0)
                    result.Add(current);

                if (font.MeasureText(word) <= maxWidth)
                {
                    current = word;
                    continue;
                }

                var slice = word;
                while (slice.Length > 0)
                {
                    var fit = 1;
                    while (fit < slice.Length && font.MeasureText(slice[..(fit + 1)]) <= maxWidth)
                        fit++;
                    result.Add(slice[..fit]);
                    slice = slice[fit..];
                }

                current = string.Empty;
            }

            if (current.Length > 0)
                result.Add(current);
        }

        return result;
    }
}
