using System.Numerics;

namespace RtsLite.Game;

internal sealed class RtsBuilding
{
    public required RtsBuildingType Type { get; init; }
    public required UnitTeam Team { get; init; }
    public int GridX { get; init; }
    public int GridZ { get; init; }

    public int Width => Type.FootprintW();
    public int Height => Type.FootprintH();

    public Vector3 WorldCenter(RtsArena arena) =>
        new(
            (GridX + Width * 0.5f) * RtsArena.CellSize,
            0f,
            (GridZ + Height * 0.5f) * RtsArena.CellSize);

    public float BillboardScale => Type switch
    {
        RtsBuildingType.ConstructionYard => 2.8f,
        RtsBuildingType.PowerPlant => 1.2f,
        RtsBuildingType.Barracks => 2.1f,
        RtsBuildingType.OreRefinery => 2.2f,
        RtsBuildingType.WarFactory => 2.4f,
        _ => 1.5f,
    };
}
