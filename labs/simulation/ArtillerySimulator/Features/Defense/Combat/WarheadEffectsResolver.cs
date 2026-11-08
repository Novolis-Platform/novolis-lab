using System.Numerics;

namespace ArtillerySimulator;

internal static class WarheadEffectsResolver
{
    public static void ResolveImpact(DefenseBattlefieldState state, Vector3 point)
    {
        state.LastBracket = 0;
        state.LastPinned = 0;
        for (var d = 0; d < state.Defenders.Count; d++)
        {
            var defender = state.Defenders[d];
            if (!defender.Alive)
                continue;

            var dx = defender.X - point.X;
            var dz = defender.Z - point.Z;
            if (MathF.Sqrt(dx * dx + dz * dz) > state.CaughtMeters)
                continue;

            defender.Alive = false;
            state.Defenders[d] = defender;
            state.FratricideLoss = true;
            state.ObjectiveLost = true;
        }

        for (var i = state.Units.Count - 1; i >= 0; i--)
        {
            var unit = state.Units[i];
            var dx = unit.X - point.X;
            var dz = unit.Z - point.Z;
            var distance = MathF.Sqrt(dx * dx + dz * dz);
            if (distance > state.PinnedMeters)
                continue;

            if (distance <= state.CaughtMeters)
            {
                ColumnConvoySimulator.ArchiveUnit(state, i, ref unit);
                state.LastBracket++;
                continue;
            }

            unit.Hold = 0f;
            unit.Scare = MathF.Max(unit.Scare, state.Piece.Warhead.ScareSeconds);
            unit.Posture = "pinned";
            state.Units[i] = unit;
            state.LastPinned++;
        }

        ThreatGeometryQueries.RefreshNearest(state);
    }
}
