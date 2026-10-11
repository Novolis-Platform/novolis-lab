using System.Numerics;
using Novolis.Rendering.Planar;

namespace TopDownDoom.Game.ImportedDoom;

internal sealed class ImportedDoomDoor
{
    public required int SourceLineIndex { get; init; }

    public required short Tag { get; init; }

    public required short Special { get; init; }

    public required Vector3 Center { get; init; }

    public required ImportedDoomWallSegment Segment { get; init; }

    public required PlanarCollider Collider { get; init; }

    public required PlanarStaticPolygon[] Visuals { get; init; }

    public bool IsOpen { get; set; }
}
