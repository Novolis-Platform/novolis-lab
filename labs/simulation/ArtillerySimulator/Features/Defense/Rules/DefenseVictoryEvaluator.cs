namespace ArtillerySimulator;

internal static class DefenseVictoryEvaluator
{
    public static bool EvaluateHeld(DefenseBattlefieldState state) =>
        !state.ObjectiveLost
        && !state.FratricideLoss
        && state.DefendersAlive > 0
        && state.Round >= 8
        && state.Units.Count == 0
        && LaneTrafficModel.ApproachesBlocked(state);

    public static MissionOutcome Outcome(DefenseBattlefieldState state)
    {
        if (state.FratricideLoss)
            return MissionOutcome.Fratricide;
        if (state.ObjectiveLost)
            return MissionOutcome.ObjectiveLost;
        if (Held(state))
            return MissionOutcome.Held;
        return MissionOutcome.Open;
    }

    public static bool Held(DefenseBattlefieldState state) =>
        state.HeldLatched || EvaluateHeld(state);
}
