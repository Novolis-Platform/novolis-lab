using System.Numerics;

namespace ArtillerySimulator;

internal static class ThreatReportBuilder
{
    public static DefenderReport[] DefenderReports(DefenseBattlefieldState state)
    {
        var rows = new DefenderReport[state.Defenders.Count];
        for (var i = 0; i < state.Defenders.Count; i++)
        {
            var defender = state.Defenders[i];
            rows[i] = new DefenderReport
            {
                Label = defender.Label,
                X = defender.X,
                Z = defender.Z,
                Alive = defender.Alive,
            };
        }

        return rows;
    }

    public static ThreatReport[] Reports(DefenseBattlefieldState state, Vector3 gun)
    {
        var reports = new ThreatReport[state.Units.Count + state.Wrecks.Count];
        var n = 0;
        void Add(ColumnUnit unit)
        {
            var dx = unit.X - gun.X;
            var dz = unit.Z - gun.Z;
            var bearing = MathF.Atan2(dz, dx) * (180f / MathF.PI);
            reports[n++] = new ThreatReport
            {
                Id = unit.Id,
                Road = state.Lanes[unit.Lane].Name,
                X = unit.X,
                Z = unit.Z,
                Alive = unit.Alive,
                InArc = MathF.Abs(OpeningFirePlan.DeltaDegrees(bearing, state.TrailBearingDegrees))
                         <= state.Piece.MaxTraverseDegrees,
                Posture = unit.Alive ? unit.Posture : "stopped",
                MetersFromGun = MathF.Sqrt(dx * dx + dz * dz),
                BearingDegrees = bearing,
                MetersToCrossing = Vector2.Distance(
                    new Vector2(unit.X, unit.Z),
                    new Vector2(state.Objective.X, state.Objective.Z)),
            };
        }

        foreach (var unit in state.Units)
            Add(unit);
        foreach (var wreck in state.Wrecks)
            Add(wreck);

        return reports;
    }
}
