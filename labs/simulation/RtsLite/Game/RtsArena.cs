using System.Numerics;
using Novolis.Math.Arrays;

namespace RtsLite.Game;

internal sealed class RtsArena
{
    public const uint GridSize = 40;
    public const float CellSize = 1f;

    public DenseGrid<byte> Walls { get; }
    public DenseGrid<byte> Tiberium { get; }

    private readonly List<RtsBuilding> _buildings = [];

    public IReadOnlyList<RtsBuilding> Buildings => _buildings;

    public Vector3 SpawnPlayer => CellCenter(5, 5);
    public Vector3 SpawnEnemy => CellCenter(GridSize - 8, GridSize - 8);

    private RtsArena(DenseGrid<byte> walls, DenseGrid<byte> tiberium)
    {
        Walls = walls;
        Tiberium = tiberium;
    }

    public static RtsArena Create()
    {
        var grid = new DenseGrid<byte>(GridSize, GridSize);
        var tiberium = new DenseGrid<byte>(GridSize, GridSize);
        for (var z = 0u; z < GridSize; z++)
        for (var x = 0u; x < GridSize; x++)
        {
            var border = x < 2 || z < 2 || x >= GridSize - 2 || z >= GridSize - 2;
            var pond = x is >= 16 and <= 22 && z is >= 16 and <= 22;
            grid[x, z, 0] = border || pond ? (byte)1 : (byte)0;

            if (!border && !pond && (x + z) % 7 == 0)
                tiberium[x, z, 0] = 1;
        }

        var arena = new RtsArena(grid, tiberium);
        arena.SeedBases();
        return arena;
    }

    public Vector3 CellCenter(uint gx, uint gz) =>
        new((gx + 0.5f) * CellSize, 0f, (gz + 0.5f) * CellSize);

    public Vector3 CellCenter(int gx, int gz) =>
        new((gx + 0.5f) * CellSize, 0f, (gz + 0.5f) * CellSize);

    public void WorldToGrid(Vector3 world, out int gx, out int gz)
    {
        gx = (int)(world.X / CellSize);
        gz = (int)(world.Z / CellSize);
    }

    public bool IsBlocked(float x, float z)
    {
        var gx = (int)(x / CellSize);
        var gz = (int)(z / CellSize);
        if (gx < 0 || gz < 0 || gx >= Walls.Width || gz >= Walls.Height)
            return true;
        if (Walls[(uint)gx, (uint)gz, 0] != 0)
            return true;
        return OccupiesBuilding(gx, gz);
    }

    public bool CanPlace(RtsBuildingType type, int gx, int gz, UnitTeam team)
    {
        var w = type.FootprintW();
        var h = type.FootprintH();
        if (gx < 1 || gz < 1 || gx + w >= Walls.Width - 1 || gz + h >= Walls.Height - 1)
            return false;

        for (var dz = 0; dz < h; dz++)
        for (var dx = 0; dx < w; dx++)
        {
            var cx = gx + dx;
            var cz = gz + dz;
            if (Walls[(uint)cx, (uint)cz, 0] != 0)
                return false;
            if (OccupiesBuilding(cx, cz))
                return false;
        }

        if (type == RtsBuildingType.ConstructionYard)
            return true;

        return HasBuilding(team, RtsBuildingType.ConstructionYard);
    }

    public bool TryPlace(RtsBuildingType type, int gx, int gz, UnitTeam team)
    {
        if (!CanPlace(type, gx, gz, team))
            return false;

        _buildings.Add(new RtsBuilding { Type = type, Team = team, GridX = gx, GridZ = gz });
        return true;
    }

    public bool OccupiesBuilding(int gx, int gz)
    {
        foreach (var b in _buildings)
        {
            if (gx >= b.GridX && gx < b.GridX + b.Width && gz >= b.GridZ && gz < b.GridZ + b.Height)
                return true;
        }

        return false;
    }

    public bool HasBuilding(UnitTeam team, RtsBuildingType type) =>
        _buildings.Any(b => b.Team == team && b.Type == type);

    private void SeedBases()
    {
        TryPlace(RtsBuildingType.ConstructionYard, 4, 4, UnitTeam.Player);
        TryPlace(RtsBuildingType.PowerPlant, 8, 4, UnitTeam.Player);
        TryPlace(RtsBuildingType.Barracks, 4, 8, UnitTeam.Player);
        TryPlace(RtsBuildingType.OreRefinery, 8, 8, UnitTeam.Player);

        var ex = (int)GridSize - 10;
        var ez = (int)GridSize - 10;
        TryPlace(RtsBuildingType.ConstructionYard, ex, ez, UnitTeam.Enemy);
        TryPlace(RtsBuildingType.PowerPlant, ex + 4, ez, UnitTeam.Enemy);
        TryPlace(RtsBuildingType.Barracks, ex, ez + 4, UnitTeam.Enemy);
        TryPlace(RtsBuildingType.WarFactory, ex + 4, ez + 4, UnitTeam.Enemy);
    }
}
