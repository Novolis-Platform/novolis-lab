using System.Numerics;

namespace FrankMoat.Levels;

internal readonly record struct WallSegment(
    Vector3 Start,
    Vector3 End,
    float Height,
    WallMaterial Material);
