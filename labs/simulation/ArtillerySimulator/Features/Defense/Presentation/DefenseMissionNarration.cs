using System.Globalization;

namespace ArtillerySimulator;

internal static class DefenseMissionNarration
{
    public static string HudLine(DefenseBattlefieldState state) => DefenseVictoryEvaluator.Outcome(state) switch
    {
        MissionOutcome.Fratricide => "FRATRICIDE — city lost",
        MissionOutcome.ObjectiveLost => "CROSSING LOST",
        MissionOutcome.Held => $"CITY HELD    {state.DefendersAlive} defenders    {state.Wrecks.Count} stopped",
        _ => $"ROUND {state.Round}  CROSSING {state.WaveSerial}  {state.Units.Count}  {PostureLine(state)}  NEAREST {state.NearestMeters / 1000f:F1} KM",
    };

    public static string Observation(DefenseBattlefieldState state)
    {
        if (state.FratricideLoss)
            return "A defender was hit. The city is lost.";
        if (state.ObjectiveLost)
            return "The downtown hold is lost.";
        if (DefenseVictoryEvaluator.Held(state))
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Kristiansand held. {state.DefendersAlive} defenders safe, {state.Wrecks.Count} hostiles stopped.");

        var avenue = state.Approaches.Count == 0 ? "the primary roads" : string.Join(", ", state.Approaches);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Defend Kvadraturen. Round {state.Round}, {state.Units.Count} advancing on {avenue}, nearest {state.NearestMeters / 1000f:F2} km, {state.DefendersAlive} defenders.");
    }

    public static string ResultPhrase(DefenseBattlefieldState state)
    {
        if (state.ObjectiveLost)
            return "The crossing is lost.";
        if (DefenseVictoryEvaluator.Held(state))
            return string.Create(
                CultureInfo.InvariantCulture,
                $"The crossing held. {state.Wrecks.Count} stopped, the roads are blocked.");

        return string.Create(
            CultureInfo.InvariantCulture,
            $"The burst put {state.LastBracket} out of action and pinned {state.LastPinned}. {state.Units.Count} still moving, nearest {state.NearestMeters / 1000f:F2} km.");
    }

    public static string PostureToken(DefenseBattlefieldState state)
    {
        var pinned = 0;
        var passing = 0;
        var spreading = 0;
        foreach (var unit in state.Units)
        {
            if (!unit.Alive)
                continue;
            if (unit.Posture == "pinned")
                pinned++;
            else if (unit.Posture == "passing")
                passing++;
            else if (unit.Posture == "spreading")
                spreading++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{pinned}.{passing}.{spreading}");
    }

    public static string PostureLine(DefenseBattlefieldState state)
    {
        var advancing = 0;
        var pinned = 0;
        var other = 0;
        foreach (var unit in state.Units)
        {
            if (!unit.Alive)
                continue;
            if (unit.Posture == "pinned")
                pinned++;
            else if (unit.Posture == "advancing")
                advancing++;
            else
                other++;
        }

        return other == 0
            ? $"{advancing} ADVANCING  {pinned} SUPPRESSED"
            : $"{advancing} ADVANCING  {pinned} SUPPRESSED  {other} OTHER";
    }
}
