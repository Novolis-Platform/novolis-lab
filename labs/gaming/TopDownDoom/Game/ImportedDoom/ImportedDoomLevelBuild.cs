using System.Numerics;

namespace TopDownDoom.Game.ImportedDoom;

internal sealed class ImportedDoomLevelBuild
{
    public required ImportedDoomPackage Package { get; init; }

    public required ImportedDoomTextureCatalog Textures { get; init; }

    public required ImportedDoomWallSegment[] WallSegments { get; init; }

    public required ImportedDoomThingSpawn[] Things { get; init; }

    public required Vector3 PlayerStart { get; init; }

    public required short PlayerAngle { get; init; }

    public required Vector3[] ExitPositions { get; init; }

    public required ImportedDoomDoor[] Doors { get; init; }
}
