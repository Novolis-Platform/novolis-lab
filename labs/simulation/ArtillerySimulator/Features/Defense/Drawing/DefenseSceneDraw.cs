using System.Drawing;
using System.Numerics;

namespace ArtillerySimulator;

/// <summary>Wire presentation for the defense scenario (objective, columns, defenders, approach ribbons).</summary>
internal static class DefenseSceneDraw
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
        wires.Line(mark, mark + Vector3.UnitY * 120f, copper);
        DrawRing(wires, mark + Vector3.UnitY * 2f, objectiveRadiusMeters, copper);

        var route = Color.FromArgb(200, 255, 120, 48);
        var hostile = Color.FromArgb(255, 255, 48, 32);
        var hostileHot = Color.FromArgb(255, 255, 210, 64);
        var wreckColor = Color.FromArgb(180, 130, 120, 110);
        var friendly = Color.FromArgb(255, 72, 210, 120);

        DrawUnits(wires, terrain, units, lanes, hostile, hostileHot, wreckColor);
        DrawUnits(wires, terrain, wrecks, lanes, hostile, hostileHot, wreckColor);

        foreach (var lane in lanes)
            DrawLaneRibbon(wires, terrain, lane, route);

        foreach (var defender in defenders)
        {
            if (!defender.Alive)
                continue;

            var at = new Vector3(defender.X, terrain.Field.SampleHeight(defender.X, defender.Z) + 3f, defender.Z);
            NatoSymbolDraw.FriendlyInfantry(wires, at, defender.YawDegrees, 22f, friendly);
        }
    }

    private static void DrawUnits(
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
            var lift = unit.Alive ? 4.5f : 2f;
            var at = new Vector3(unit.X, y + lift, unit.Z);
            var lane = lanes[unit.Lane];
            var yaw = LaneYaw(lane, unit.Arc);
            var color = unit.Alive
                ? (unit.Posture is "holding" or "pinned" or "blocked" or "passing" ? hostileHot : hostile)
                : wreckColor;
            NatoSymbolDraw.HostileMechanized(wires, at, yaw, 42f, color, wreck: !unit.Alive);
        }
    }

    private static void DrawLaneRibbon(SilkWireBatch wires, TerrainWorld terrain, RouteLane lane, Color color)
    {
        if (lane.Ribbon is { Length: >= 2 } ribbon)
        {
            for (var i = 1; i < ribbon.Length; i++)
                wires.Line(ribbon[i - 1], ribbon[i], color);
            return;
        }

        const float step = 80f;
        Vector3? previous = null;
        for (var arc = 0f; arc <= lane.Line.Length; arc += step)
        {
            var p = lane.Line.At(Math.Min(arc, lane.Line.Length));
            var at = new Vector3(p.X, terrain.Field.SampleHeight(p.X, p.Y) + 2.5f, p.Y);
            if (previous is { } from)
                wires.Line(from, at, color);
            previous = at;
        }
    }

    public static Vector3[] BuildRibbon(TerrainWorld terrain, GroundPolyline line, float step = 80f)
    {
        var count = Math.Max(2, (int)MathF.Ceiling(line.Length / step) + 1);
        var ribbon = new Vector3[count];
        for (var i = 0; i < count; i++)
        {
            var arc = Math.Min(line.Length, i * step);
            var p = line.At(arc);
            ribbon[i] = new Vector3(p.X, terrain.Field.SampleHeight(p.X, p.Y) + 2.5f, p.Y);
        }

        return ribbon;
    }

    private static float LaneYaw(RouteLane lane, float arc)
    {
        var point = lane.Line.At(arc);
        var probe = lane.Line.At(Math.Clamp(arc - lane.Away * 20f, 0f, lane.Line.Length));
        var forward = probe - point;
        if (forward.LengthSquared() < 1f)
            return 0f;

        return MathF.Atan2(forward.Y, forward.X) * (180f / MathF.PI);
    }

    private static void DrawRing(SilkWireBatch wires, Vector3 center, float radius, Color color)
    {
        const int segments = 24;
        var previous = center + new Vector3(radius, 0f, 0f);
        for (var i = 1; i <= segments; i++)
        {
            var angle = i / (float)segments * MathF.Tau;
            var next = center + new Vector3(MathF.Cos(angle) * radius, 0f, MathF.Sin(angle) * radius);
            wires.Line(previous, next, color);
            previous = next;
        }
    }
}
