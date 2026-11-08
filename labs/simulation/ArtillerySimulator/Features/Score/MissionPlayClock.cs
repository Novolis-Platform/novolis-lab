using System.Globalization;

namespace ArtillerySimulator;

/// <summary>
/// Play timer: battle-clock seconds from the mission plus wall time spent in live play.
/// </summary>
internal sealed class MissionPlayClock
{
    private float _wallPlaySeconds;

    public float BattleSeconds { get; private set; }
    public float WallPlaySeconds => _wallPlaySeconds;

    public void Reset()
    {
        BattleSeconds = 0f;
        _wallPlaySeconds = 0f;
    }

    public void SyncBattle(float battleSeconds) =>
        BattleSeconds = MathF.Max(0f, battleSeconds);

    public void AdvanceWall(float wallSeconds, bool playing)
    {
        if (playing && wallSeconds > 0f)
            _wallPlaySeconds += wallSeconds;
    }

    public static string Format(float seconds)
    {
        if (seconds < 0f)
            seconds = 0f;
        var total = (int)seconds;
        var m = total / 60;
        var s = total % 60;
        return string.Create(CultureInfo.InvariantCulture, $"{m:00}:{s:00}");
    }
}
