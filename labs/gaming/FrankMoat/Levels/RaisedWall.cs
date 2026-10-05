using Novolis.Math.Topology;

namespace FrankMoat.Levels;

internal sealed class RaisedWall
{
    public required Polygon Footprint { get; init; }

    public required float Height { get; init; }

    public required WallMaterial Material { get; init; }

    public required IReadOnlyList<WallSegment> Segments { get; init; }
}
