namespace ArtillerySimulator;

/// <summary>Educational arcade point table — not a real fire-control grading scheme.</summary>
internal static class ScoreRules
{
    public const int PointsPerKill = 100;
    public const int PointsPerPin = 25;
    public const int PointsPerRoundCleared = 500;
    public const int PointsPerDefenderAlive = 150;
    public const int HeldBonus = 2_000;
    /// <summary>Bonus for finishing under this battle-time target (seconds).</summary>
    public const float HeldTimeTargetSeconds = 20 * 60f;
    public const int HeldTimeBonusPerSecondUnder = 2;
    public const int FratricidePenalty = -2_500;
    public const int ObjectiveLostPenalty = -1_000;
}
