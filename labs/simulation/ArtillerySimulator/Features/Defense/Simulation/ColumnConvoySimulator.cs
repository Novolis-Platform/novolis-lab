using System.Numerics;

namespace ArtillerySimulator;

internal static class ColumnConvoySimulator
{
    public static void AdvanceUnits(DefenseBattlefieldState state, float simSeconds)
    {
        for (var i = state.Units.Count - 1; i >= 0; i--)
        {
            var unit = state.Units[i];
            var lane = state.Lanes[unit.Lane];
            var gap = LaneTrafficModel.GapAhead(state, unit, lane, out var wreckAhead);

            if (unit.Scare > 0f)
                unit.Scare = MathF.Max(0f, unit.Scare - simSeconds);

            var suppressed = unit.Scare > 0f;
            var jitter = 0.92f + (unit.Id % 7) * 0.025f;
            var speed = (suppressed ? state.ColumnSpeed * 0.35f : state.ColumnSpeed) * jitter;
            var posture = suppressed ? "pinned" : "advancing";

            if (wreckAhead && gap < DefenseBattlefieldState.SpacingMeters * 0.9f && !unit.Passing)
            {
                if (gap < DefenseBattlefieldState.SpacingMeters * 0.55f && LaneTrafficModel.CanStartPass(unit, lane, gap))
                {
                    unit.Passing = true;
                    posture = "passing";
                }
                else
                {
                    unit.Posture = "blocked";
                    unit.Offset *= MathF.Max(0f, 1f - 2f * simSeconds);
                    state.Units[i] = unit;
                    continue;
                }
            }

            if (unit.Passing && gap > 160f)
                unit.Passing = false;
            else if (!wreckAhead && gap < DefenseBattlefieldState.SpacingMeters * 0.65f && gap < 90f
                     && LaneTrafficModel.CanStartPass(unit, lane, gap))
            {
                unit.Passing = true;
                posture = "passing";
            }

            var desiredOffset = 0f;
            if (unit.Passing)
            {
                desiredOffset = (unit.Id & 1) == 0 ? 5.5f : -5.5f;
                posture = "passing";
            }
            else if (suppressed)
                desiredOffset = (unit.Id & 1) == 0 ? -4f : 4f;

            var step = 8f * simSeconds;
            unit.Offset += Math.Clamp(desiredOffset - unit.Offset, -step, step);
            unit.Offset = Math.Clamp(unit.Offset, -6.5f, 6.5f);

            if (wreckAhead && unit.Passing)
                speed *= 0.55f;

            var next = unit.Arc - lane.Away * speed * simSeconds;
            if (MathF.Abs(next - lane.CrossingArc) <= DefenseBattlefieldState.ObjectiveRadiusMeters)
            {
                PlaceOnLane(state, lane, ref unit);
                unit.Posture = posture;
                state.Units[i] = unit;
                state.ObjectiveLost = true;
                ThreatGeometryQueries.RefreshNearest(state);
                return;
            }

            var probe = lane.Line.At(next);
            if (!PlayableMapBounds.InPlayableMap(state.Terrain, probe.X, probe.Y))
            {
                ArchiveUnit(state, i, ref unit);
                continue;
            }

            unit.Arc = next;
            PlaceOnLane(state, lane, ref unit);
            if (!PlayableMapBounds.InPlayableMap(state.Terrain, unit.X, unit.Z))
                ArchiveUnit(state, i, ref unit);
            else
            {
                unit.Posture = posture;
                state.Units[i] = unit;
            }
        }

        ThreatGeometryQueries.RefreshNearest(state);
    }

    public static void PlaceOnLane(DefenseBattlefieldState state, RouteLane lane, ref ColumnUnit unit)
    {
        var point = lane.Line.At(unit.Arc);
        var probe = lane.Line.At(unit.Arc - lane.Away * 16f);
        var forward = probe - point;
        if (forward.LengthSquared() < 1f)
            forward = new Vector2(lane.Away, 0f);

        forward = Vector2.Normalize(forward);
        var side = new Vector2(-forward.Y, forward.X);
        var placed = point + side * unit.Offset;
        unit.X = PlayableMapBounds.ClampX(state.Terrain, placed.X);
        unit.Z = PlayableMapBounds.ClampZ(state.Terrain, placed.Y);
    }

    public static void ArchiveUnit(DefenseBattlefieldState state, int index, ref ColumnUnit unit)
    {
        unit.Alive = false;
        unit.Posture = "stopped";
        unit.Passing = false;
        unit.Scare = 0f;
        unit.Offset = 0f;
        state.Wrecks.Add(unit);
        if (index >= 0 && index < state.Units.Count)
            state.Units.RemoveAt(index);

        while (state.Wrecks.Count > DefenseBattlefieldState.MaxTrackedWrecks)
            state.Wrecks.RemoveAt(0);
    }
}
