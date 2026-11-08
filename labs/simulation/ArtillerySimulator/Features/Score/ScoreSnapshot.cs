namespace ArtillerySimulator;

/// <summary>Immutable scoreboard readout for HUD / agent / pause.</summary>
internal readonly record struct ScoreSnapshot(
    int Points,
    int HighScore,
    bool IsNewHighScore,
    int Shots,
    int Kills,
    int Pins,
    int RoundsCleared,
    float BattleSeconds,
    float WallPlaySeconds,
    MissionOutcome Outcome,
    bool Finalized)
{
    public string TimeText => MissionPlayClock.Format(BattleSeconds);

    public string HudLine =>
        Finalized
            ? $"SCORE {Points}  HI {HighScore}{(IsNewHighScore ? " *" : "")}  {TimeText}"
            : $"SCORE {Points}  HI {HighScore}  {TimeText}";

    public string PauseBody =>
        Finalized
            ? $"FINAL  {Points} pts{(IsNewHighScore ? "  — NEW HIGH SCORE" : "")}\n" +
              $"TIME   {TimeText}   SHOTS {Shots}   KILLS {Kills}   PINS {Pins}\n" +
              $"ROUNDS CLEARED {RoundsCleared}   HI {HighScore}\n" +
              $"OUTCOME {Outcome}"
            : $"SCORE  {Points}   HI {HighScore}\n" +
              $"TIME   {TimeText}   SHOTS {Shots}   KILLS {Kills}   PINS {Pins}";
}
