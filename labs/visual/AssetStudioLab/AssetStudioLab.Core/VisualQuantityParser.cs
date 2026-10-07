using System.Globalization;

namespace AssetStudioLab;

/// <summary>Parses <c>18mm</c>, <c>44deg</c>, <c>90ms</c>, <c>5100K</c>, <c>1700lm</c>, <c>160/s</c>.</summary>
public static class VisualQuantityParser
{
    public static bool TryParse(double magnitude, string unitText, out VisualValue value)
    {
        value = default!;
        var unit = unitText.Trim().ToLowerInvariant();
        switch (unit)
        {
            case "mm":
                value = VisualValue.FromDistance(magnitude / 1000.0, VisualUnit.Millimeters);
                return true;
            case "cm":
                value = VisualValue.FromDistance(magnitude / 100.0, VisualUnit.Centimeters);
                return true;
            case "m":
                value = VisualValue.FromDistance(magnitude, VisualUnit.Meters);
                return true;
            case "deg":
                value = VisualValue.FromAngle(magnitude * Math.PI / 180.0, VisualUnit.Degrees);
                return true;
            case "rad":
                value = VisualValue.FromAngle(magnitude, VisualUnit.Radians);
                return true;
            case "ms":
                value = VisualValue.FromTime(magnitude / 1000.0, VisualUnit.Milliseconds);
                return true;
            case "s":
                value = VisualValue.FromTime(magnitude, VisualUnit.Seconds);
                return true;
            case "k":
                value = VisualValue.FromTemperature(magnitude);
                return true;
            case "lm":
                value = VisualValue.FromScalar(magnitude, VisualUnit.Lumens);
                return true;
            case "/s":
            case "persecond":
                value = VisualValue.FromScalar(magnitude, VisualUnit.PerSecond);
                return true;
            default:
                return false;
        }
    }

    public static string Format(VisualValue value)
    {
        var n = value.Scalar.ToString("0.###", CultureInfo.InvariantCulture);
        return value.Kind switch
        {
            VisualKind.Distance => value.Unit switch
            {
                VisualUnit.Millimeters => (value.Scalar * 1000.0).ToString("0.###", CultureInfo.InvariantCulture) + "mm",
                VisualUnit.Centimeters => (value.Scalar * 100.0).ToString("0.###", CultureInfo.InvariantCulture) + "cm",
                _ => n + "m",
            },
            VisualKind.Angle => value.Unit == VisualUnit.Degrees
                ? (value.Scalar * 180.0 / Math.PI).ToString("0.###", CultureInfo.InvariantCulture) + "deg"
                : n + "rad",
            VisualKind.Time => value.Unit == VisualUnit.Milliseconds
                ? (value.Scalar * 1000.0).ToString("0.###", CultureInfo.InvariantCulture) + "ms"
                : n + "s",
            VisualKind.Temperature => n + "K",
            _ when value.Unit == VisualUnit.Lumens => n + "lm",
            _ when value.Unit == VisualUnit.PerSecond => n + "/s",
            VisualKind.Color => FormatColor(value),
            VisualKind.Vector3 => $"{value.X.ToString("0.###", CultureInfo.InvariantCulture)}, {value.Y.ToString("0.###", CultureInfo.InvariantCulture)}, {value.Z.ToString("0.###", CultureInfo.InvariantCulture)}",
            _ => n,
        };
    }

    public static string FormatColor(VisualValue value)
    {
        var c = value.ToRgba32();
        return $"#{c.R:x2}{c.G:x2}{c.B:x2}";
    }
}
