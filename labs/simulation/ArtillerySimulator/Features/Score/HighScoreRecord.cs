namespace ArtillerySimulator;

internal sealed class HighScoreRecord
{
    public required string LevelId { get; init; }
    public int Points { get; init; }
    public float BattleSeconds { get; init; }
    public string Outcome { get; init; } = "";
    public string SeedHash { get; init; } = "";
    public DateTimeOffset WhenUtc { get; init; }
}
