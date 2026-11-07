namespace ArtillerySimulator;

/// <summary>Battery radio phrase bank (original dialogue; military-sim tone).</summary>
internal sealed class BatteryChatter
{
    private static readonly string[] IdleLines =
    [
        "Battery, report status.",
        "FO, standing by for fire mission.",
        "Gun one ready — breech closed.",
        "Met check complete. Wind from the south-west.",
        "Maintain observation on the approaches.",
        "Check fire lanes clear of friendlies.",
        "Howitzer on the line. Waiting for orders.",
        "Keep eyes on the harbour road.",
    ];

    private static readonly string[] FireLines =
    [
        "Shot, over.",
        "Gun one, firing.",
        "Round away.",
        "Battery, shot.",
    ];

    private static readonly string[] SplashLines =
    [
        "Splash, over.",
        "Impact observed.",
        "Good effect on target.",
        "Airburst confirmed.",
        "Repeat, fire for effect.",
    ];

    private static readonly string[] AlarmLines =
    [
        "Enemy column advancing — engage!",
        "Crossing under threat — fire mission now!",
        "Fratricide risk — check your MPI!",
        "City sector under pressure.",
    ];

    private static readonly string[] VictoryLines =
    [
        "City held. End of mission.",
        "Objective secure. Cease fire.",
    ];

    private static readonly string[] DefeatLines =
    [
        "Crossing lost. Mission failed.",
        "Fratricide — cease fire immediately.",
    ];

    private readonly Random _rng;
    private float _idleTimer = 10f;
    private string? _currentLine;
    private float _lineTimer;
    private string _speaker = "FO";

    public BatteryChatter(Random rng) => _rng = rng;

    public string? CurrentLine => _lineTimer > 0 ? _currentLine : null;

    public string? CurrentSpeaker => _lineTimer > 0 ? _speaker : null;

    public string? Update(float dt, bool missionHot, bool terminalVictory, bool terminalDefeat)
    {
        _lineTimer = Math.Max(0, _lineTimer - dt);
        if (_lineTimer <= 0)
            _currentLine = null;

        if (terminalVictory)
            return Push(VictoryLines, "HQ");
        if (terminalDefeat)
            return Push(DefeatLines, "HQ");

        string? announced = null;
        if (missionHot && _rng.NextDouble() < 0.004)
            announced = Push(AlarmLines, "FO");

        _idleTimer -= dt;
        if (announced is null && _idleTimer <= 0 && _lineTimer <= 0)
        {
            announced = Push(IdleLines, PickSpeaker());
            _idleTimer = 16f + (float)_rng.NextDouble() * 18f;
        }

        return announced;
    }

    public string AnnounceFire() => Push(FireLines, "GUN ONE");

    public string AnnounceSplash(bool airburst) =>
        Push(airburst ? ["Airburst confirmed.", "Splash, airburst."] : SplashLines, "FO");

    public string AnnounceMissionStart() =>
        Push(["Battery, this is FO — fire mission, danger close.", "Defend the harbour. Engage on my mark."], "FO");

    private string Push(string[] pool, string speaker)
    {
        _currentLine = pool[_rng.Next(pool.Length)];
        _speaker = speaker;
        _lineTimer = 5.2f;
        return _currentLine;
    }

    private string PickSpeaker() =>
        _rng.Next(3) switch
        {
            0 => "FO",
            1 => "GUN ONE",
            _ => "HQ",
        };
}
