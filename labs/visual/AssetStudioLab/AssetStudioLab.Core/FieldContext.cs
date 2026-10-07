using System.Numerics;

namespace AssetStudioLab;

/// <summary>Evaluation context for a field sample.</summary>
public sealed record FieldContext(
    Vector3 Position,
    Vector3 Normal,
    Vector2 Uv,
    float Time,
    float Age,
    Vector3 Velocity,
    float Distance,
    Vector3 ViewDirection,
    float Seed);
