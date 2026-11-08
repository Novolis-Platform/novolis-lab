using System.Drawing;
using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// Oslofjorden presentation: objective + ships. Fairways stay invisible; decorative roads
/// come from the terrain static mesh only.
/// </summary>
internal static class NavalSceneDraw
{
    public static void Draw(
        SilkWireBatch wires,
        TerrainWorld terrain,
        Vector3 objective,
        float objectiveRadiusMeters,
        IReadOnlyList<ColumnUnit> units,
        IReadOnlyList<ColumnUnit> wrecks,
        IReadOnlyList<RouteLane> lanes,
        IReadOnlyList<DefenderUnit> defenders)
    {
        var ground = terrain.Field.SampleHeight(objective.X, objective.Z);
        var mark = new Vector3(objective.X, ground, objective.Z);
        var copper = Color.FromArgb(255, 232, 196, 120);
        wires.Line(mark, mark + Vector3.UnitY * 140f, copper);
        wires.Ring(mark + Vector3.UnitY * 2f, objectiveRadiusMeters, copper, segments: 28, yLift: 0f);

        var hostile = Color.FromArgb(255, 255, 48, 32);
        var suppressed = Color.FromArgb(255, 255, 210, 64);
        var wreckColor = Color.FromArgb(180, 130, 120, 110);
        var friendly = Color.FromArgb(255, 72, 210, 120);

        DrawShips(wires, terrain, units, lanes, hostile, suppressed, wreckColor);
        DrawShips(wires, terrain, wrecks, lanes, hostile, suppressed, wreckColor);

        foreach (var defender in defenders)
        {
            if (!defender.Alive)
                continue;

            var at = new Vector3(defender.X, terrain.Field.SampleHeight(defender.X, defender.Z) + 3f, defender.Z);
            NatoSymbolDraw.FriendlyArtillery(wires, at, defender.YawDegrees, 26f, friendly);
        }
    }

    private static void DrawShips(
        SilkWireBatch wires,
        TerrainWorld terrain,
        IReadOnlyList<ColumnUnit> units,
        IReadOnlyList<RouteLane> lanes,
        Color hostile,
        Color hostileHot,
        Color wreckColor)
    {
        foreach (var unit in units)
        {
            var y = terrain.Field.SampleHeight(unit.X, unit.Z);
            var at = new Vector3(unit.X, y + (unit.Alive ? 3f : 1.5f), unit.Z);
            var lane = lanes[unit.Lane];
            var yaw = LaneYaw(lane, unit.Arc);
            var color = unit.Alive
                ? (unit.Scare > 0f || unit.Posture is "pinned" or "holding" ? hostileHot : hostile)
                : wreckColor;
            // Capital ships read larger than destroyer escorts.
            var size = unit.Id % 5 == 1 ? 78f : 52f;
            NatoSymbolDraw.HostileWarship(wires, at, yaw, size, color, wreck: !unit.Alive);
        }
    }

    private static float LaneYaw(RouteLane lane, float arc)
    {
        var a = lane.Line.At(arc);
        var b = lane.Line.At(arc - lane.Away * 40f);
        var d = b - a;
        if (d.LengthSquared() < 1f)
            return 0f;
        return MathF.Atan2(d.Y, d.X) * (180f / MathF.PI);
    }
}
