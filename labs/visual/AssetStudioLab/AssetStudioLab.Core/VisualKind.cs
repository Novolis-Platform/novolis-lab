namespace AssetStudioLab;

/// <summary>Strong visual type. Fields wrap a payload kind.</summary>
public enum VisualKind
{
    Scalar,
    Bool,
    Color,
    Vector2,
    Vector3,
    Distance,
    Angle,
    Time,
    Temperature,
    FieldScalar,
    FieldColor,
    FieldVector3,
    Geometry,
    Surface,
    Volume,
    Light,
    Emitter,
    Effect,
    FrameEffect,
    Unknown,
}
