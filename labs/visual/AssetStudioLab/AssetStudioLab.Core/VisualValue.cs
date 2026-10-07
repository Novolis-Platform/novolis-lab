using Novolis.Math.Geometry;

namespace AssetStudioLab;

/// <summary>Typed literal payload. Quantities store SI in <see cref="Scalar"/> / <see cref="X"/>.</summary>
public sealed record VisualValue
{
    public VisualKind Kind { get; init; } = VisualKind.Scalar;
    public double Scalar { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public Rgba32 Color { get; init; }
    public bool Flag { get; init; }
    public VisualUnit Unit { get; init; }
    public string? Text { get; init; }

    public static VisualValue FromScalar(double value, VisualUnit unit = VisualUnit.None) =>
        new() { Kind = VisualKind.Scalar, Scalar = value, Unit = unit };

    public static VisualValue FromDistance(double meters, VisualUnit sourceUnit) =>
        new() { Kind = VisualKind.Distance, Scalar = meters, Unit = sourceUnit };

    public static VisualValue FromAngle(double radians, VisualUnit sourceUnit) =>
        new() { Kind = VisualKind.Angle, Scalar = radians, Unit = sourceUnit };

    public static VisualValue FromTime(double seconds, VisualUnit sourceUnit) =>
        new() { Kind = VisualKind.Time, Scalar = seconds, Unit = sourceUnit };

    public static VisualValue FromTemperature(double kelvin) =>
        new() { Kind = VisualKind.Temperature, Scalar = kelvin, Unit = VisualUnit.Kelvin };

    public static VisualValue FromColor(Rgba32 color, double r = 0, double g = 0, double b = 0) =>
        new()
        {
            Kind = VisualKind.Color,
            Color = color,
            X = r == 0 && g == 0 && b == 0 ? color.R / 255.0 : r,
            Y = r == 0 && g == 0 && b == 0 ? color.G / 255.0 : g,
            Z = r == 0 && g == 0 && b == 0 ? color.B / 255.0 : b,
        };

    public static VisualValue FromVector3(double x, double y, double z) =>
        new() { Kind = VisualKind.Vector3, X = x, Y = y, Z = z };

    public static VisualValue FromBool(bool value) =>
        new() { Kind = VisualKind.Bool, Flag = value };

    public static VisualValue FromIdent(string text) =>
        new() { Kind = VisualKind.Scalar, Text = text };

    public Rgba32 ToRgba32() =>
        Kind == VisualKind.Color
            ? new Rgba32(
                ToByte(X),
                ToByte(Y),
                ToByte(Z),
                Color.A == 0 ? (byte)255 : Color.A)
            : Color;

    private static byte ToByte(double channel) =>
        (byte)Math.Clamp((int)Math.Round(channel * 255.0), 0, 255);
}
