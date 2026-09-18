using System.Drawing;
using System.Numerics;

namespace RandoriFight.Game.Skeleton;

/// <summary>One skinned body segment (Unity-style: Hips→Spine, Arm, ForeArm, UpLeg, Leg, Foot).</summary>
internal readonly struct RigSegment(Vector3 start, Vector3 end, float radius, Color color, RigSegmentKind kind)
{
    public Vector3 Start { get; } = start;
    public Vector3 End { get; } = end;
    public float Radius { get; } = radius;
    public Color Color { get; } = color;
    public RigSegmentKind Kind { get; } = kind;
}

internal enum RigSegmentKind
{
    Torso,
    Limb,
    Head,
    Extremity,
}
