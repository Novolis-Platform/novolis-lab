using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArtillerySimulator;

/// <summary>Mission score facade: ledger + play clock + high-score persistence.</summary>
internal sealed class ScoreBoard
{
    private readonly ScoreLedger _ledger = new();
    private readonly MissionPlayClock _clock = new();
    private readonly HighScoreStore _store;
    private readonly ILogger _log;
    private string _levelId = "";
    private string _seedHash = "";
    private int _highScore;
    private bool _isNewHighScore;

    public ScoreBoard(HighScoreStore? store = null, ILogger? log = null)
    {
        _store = store ?? new HighScoreStore();
        _log = log ?? NullLogger.Instance;
    }

    public ScoreSnapshot Snapshot => new(
        _ledger.Points,
        _highScore,
        _isNewHighScore,
        _ledger.Shots,
        _ledger.Kills,
        _ledger.Pins,
        _ledger.RoundsCleared,
        _clock.BattleSeconds,
        _clock.WallPlaySeconds,
        _ledger.Outcome,
        _ledger.Finalized);

    public void BeginMission(string levelId, string seedHash)
    {
        _levelId = levelId;
        _seedHash = seedHash;
        _ledger.Reset();
        _clock.Reset();
        _isNewHighScore = false;
        _highScore = _store.BestPoints(levelId);
        _log.LogInformation("Scoreboard reset for {LevelId} (hi={HighScore})", levelId, _highScore);
    }

    public void RegisterShot() => _ledger.RegisterShot();

    public void RegisterImpact(int kills, int pins) => _ledger.RegisterImpact(kills, pins);

    public void Tick(IDefenseMission? mission, bool playing, float wallSeconds)
    {
        _clock.AdvanceWall(wallSeconds, playing);
        if (mission is null)
            return;

        _clock.SyncBattle(mission.BattleElapsedSeconds);
        _ledger.SyncRound(mission.Round);

        if (mission.IsTerminal && !_ledger.Finalized)
            Finalize(mission);
    }

    private void Finalize(IDefenseMission mission)
    {
        _ledger.Finalize(mission.Outcome, mission.DefendersAlive, mission.BattleElapsedSeconds);
        var snap = Snapshot;
        var record = new HighScoreRecord
        {
            LevelId = _levelId,
            Points = snap.Points,
            BattleSeconds = snap.BattleSeconds,
            Outcome = snap.Outcome.ToString(),
            SeedHash = _seedHash,
            WhenUtc = DateTimeOffset.UtcNow,
        };
        _isNewHighScore = _store.TrySubmit(record);
        if (_isNewHighScore)
            _highScore = snap.Points;

        _log.LogInformation(
            "Score finalized: {Points} pts time={Time} outcome={Outcome} newHi={NewHi}",
            snap.Points,
            snap.TimeText,
            snap.Outcome,
            _isNewHighScore);
        Console.WriteLine(
            $"SCORE  {snap.Points} pts  time={snap.TimeText}  shots={snap.Shots}  kills={snap.Kills}  pins={snap.Pins}" +
            (_isNewHighScore ? "  NEW HIGH SCORE" : $"  hi={_highScore}"));
    }
}
