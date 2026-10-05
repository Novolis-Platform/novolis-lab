using System.Numerics;
using FrankMoat.Levels;
using Novolis.Rendering.TwoD;

namespace FrankMoat.Rendering;

internal sealed class WallFaceSprite
{
    public required TwoDStaticPolygon Polygon { get; init; }

    public required Vector3 A { get; init; }

    public required Vector3 B { get; init; }

    public required Vector3 Extrusion { get; init; }

    public required WallMaterial Material { get; init; }

    public required bool Occludes { get; init; }
}
