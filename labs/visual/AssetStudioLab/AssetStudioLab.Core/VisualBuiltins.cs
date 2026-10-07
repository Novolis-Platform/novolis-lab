namespace AssetStudioLab;

/// <summary>Builtin call signatures for the visual type checker.</summary>
public static class VisualBuiltins
{
    public static readonly HashSet<string> ContextNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "position", "normal", "uv", "time", "age", "velocity", "distance", "viewDirection", "seed", "inherit",
    };

    public static VisualKind? ReturnKind(string name) =>
        name.ToLowerInvariant() switch
        {
            "noise" or "cavity" or "fade" or "edgewear" or "cavitygrime" => VisualKind.FieldScalar,
            "brushed" => VisualKind.FieldVector3,
            "mix" => null,
            "radial" => VisualKind.FieldColor,
            "capsule" or "box" or "bevel" or "panelize" => VisualKind.Geometry,
            "point" or "spot" => VisualKind.Light,
            "bloom" => VisualKind.FrameEffect,
            _ => VisualKind.Unknown,
        };

    public static VisualKind ExpectedArg(string function, string? argName, int index)
    {
        var fn = function.ToLowerInvariant();
        var arg = argName?.ToLowerInvariant();
        if (fn is "noise")
        {
            if (arg is "scale" || index == 0)
                return VisualKind.Distance;
            return VisualKind.Scalar;
        }

        if (fn is "brushed" && (arg is "direction" || index == 0))
            return VisualKind.Vector3;
        if (fn is "radial")
            return VisualKind.Color;
        if (fn is "mix" && index == 2)
            return VisualKind.FieldScalar;
        if (fn is "bloom")
            return VisualKind.Scalar;
        if (fn is "capsule" or "box" or "bevel" or "panelize")
            return VisualKind.Distance;
        if (fn is "point" or "spot")
            return arg is "color" || index == 0 ? VisualKind.Color : VisualKind.Scalar;
        return VisualKind.Scalar;
    }

    public static VisualKind PropertyKind(string property) =>
        property.ToLowerInvariant() switch
        {
            "color" or "basecolor" or "emission" => VisualKind.FieldColor,
            "roughness" or "metalness" or "density" or "scattering" or "amount" or "wear" or "grime"
                or "intensity" or "rate" or "penumbra" => VisualKind.FieldScalar,
            "normal" => VisualKind.FieldVector3,
            "temperature" => VisualKind.Temperature,
            "range" or "radius" or "length" or "scale" => VisualKind.Distance,
            "cone" => VisualKind.Angle,
            "lifetime" => VisualKind.Time,
            "geometry" => VisualKind.Geometry,
            "light" => VisualKind.Light,
            "bloom" => VisualKind.FrameEffect,
            _ => VisualKind.Scalar,
        };

    public static VisualKind LiftToField(VisualKind kind) =>
        kind switch
        {
            VisualKind.Color => VisualKind.FieldColor,
            VisualKind.Vector3 => VisualKind.FieldVector3,
            VisualKind.Scalar or VisualKind.Distance or VisualKind.Angle or VisualKind.Time
                or VisualKind.Temperature => VisualKind.FieldScalar,
            _ => kind,
        };

    public static bool Assignable(VisualKind from, VisualKind to)
    {
        if (from == to || to == VisualKind.Unknown || from == VisualKind.Unknown)
            return true;
        if (to == VisualKind.FieldScalar && from is VisualKind.Scalar or VisualKind.Distance or VisualKind.Angle
            or VisualKind.Time or VisualKind.Temperature or VisualKind.FieldScalar)
            return true;
        if (to == VisualKind.FieldColor && from is VisualKind.Color or VisualKind.FieldColor)
            return true;
        if (to == VisualKind.FieldVector3 && from is VisualKind.Vector3 or VisualKind.FieldVector3)
            return true;
        if (to == VisualKind.Scalar && from is VisualKind.Distance or VisualKind.Angle or VisualKind.Time
            or VisualKind.Temperature)
            return true;
        return false;
    }
}
