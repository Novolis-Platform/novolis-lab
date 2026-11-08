using System.Numerics;

namespace ArtillerySimulator;

internal static class DowntownDefenderPlacement
{
    public static void PlaceDefenders(DefenseBattlefieldState state)
    {
        AddDefender(state, "D1", DefenderAt(state, 270f, 240f));
        AddDefender(state, "D2", DefenderAt(state, 180f, 220f));
        AddDefender(state, "D3", DefenderAt(state, 0f, 220f));
        AddDefender(state, "D4", DefenderAt(state, 90f, 200f));
    }

    public static Vector2 DefenderAt(DefenseBattlefieldState state, float bearingDegrees, float meters)
    {
        var rad = bearingDegrees * (MathF.PI / 180f);
        var seed = new Vector2(
            state.Objective.X + MathF.Cos(rad) * meters,
            state.Objective.Z + MathF.Sin(rad) * meters);
        if (PrimaryRoadNetwork.Snap(seed) is { } snapped
            && Vector2.Distance(snapped, seed) < 140f)
            return snapped;
        return seed;
    }

    public static void AddDefender(DefenseBattlefieldState state, string label, Vector2 at)
    {
        state.Defenders.Add(new DefenderUnit
        {
            Label = label,
            X = at.X,
            Z = at.Y,
            YawDegrees = OpeningFirePlan.BearingDegrees(
                new Vector3(state.GunX, 0f, state.GunZ),
                new Vector3(at.X, 0f, at.Y)),
            Alive = true,
        });
    }
}
