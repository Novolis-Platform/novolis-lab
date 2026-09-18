using System.Numerics;

namespace RtsLite.Game;

internal sealed class RtsBuildPlacer
{
    public RtsBuildingType? ActiveType { get; private set; }

    public void Select(RtsBuildingType type) => ActiveType = type;

    public void Cancel() => ActiveType = null;

    public bool IsActive => ActiveType is not null;

    public void TryPlaceAtGround(RtsArena arena, Vector3 ground, UnitTeam team)
    {
        if (ActiveType is not { } type)
            return;

        arena.WorldToGrid(ground, out var gx, out var gz);
        if (arena.TryPlace(type, gx, gz, team))
            return;

        arena.WorldToGrid(ground - new Vector3(0.5f, 0f, 0.5f), out gx, out gz);
        arena.TryPlace(type, gx, gz, team);
    }

    public bool CanPlaceAt(RtsArena arena, Vector3 ground, UnitTeam team)
    {
        if (ActiveType is not { } type)
            return false;

        arena.WorldToGrid(ground, out var gx, out var gz);
        return arena.CanPlace(type, gx, gz, team);
    }

    public bool TryGetGhostFootprint(
        RtsArena arena,
        Vector3 ground,
        UnitTeam team,
        out Vector3 center,
        out float width,
        out float depth,
        out bool canPlace)
    {
        center = default;
        width = 0f;
        depth = 0f;
        canPlace = false;
        if (ActiveType is not { } type)
        {
            return false;
        }

        arena.WorldToGrid(ground, out var gx, out var gz);
        canPlace = arena.CanPlace(type, gx, gz, team);
        width = type.FootprintW() * RtsArena.CellSize;
        depth = type.FootprintH() * RtsArena.CellSize;
        center = new Vector3(
            (gx + type.FootprintW() * 0.5f) * RtsArena.CellSize,
            0f,
            (gz + type.FootprintH() * 0.5f) * RtsArena.CellSize);
        return true;
    }
}
