namespace ArtillerySimulator;

/// <summary>Battery radio phrase bank — alarm/idle gated by incoming threat pressure.</summary>
internal sealed class BatteryChatter
{
    private static readonly string[] QuietLines =
    [
        "Battery, standing by.",
        "Gun one ready — breech closed.",
        "Met check complete.",
        "Howitzer on the line.",
    ];

    private static readonly string[] ContactLines =
    [
        "Enemy column advancing — engage!",
        "Crossing under threat — fire mission now!",
        "Contact on the approaches.",
        "City sector under pressure.",
        "FO has eyes on hostiles — fire when ready.",
        "Nearest column closing. Engage!",
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
    private float _idleTimer = 14f;
    private string? _currentLine;
    private float _lineTimer;
    private string _speaker = "FO";
    private int _lastRemaining = -1;

    public BatteryChatter(Random rng) => _rng = rng;

    public string? CurrentLine => _lineTimer > 0 ? _currentLine : null;

    public string? CurrentSpeaker => _lineTimer > 0 ? _speaker : null;

    /// <param name="remaining">Living OPFOR count.</param>
    /// <param name="nearestMeters">Closest living OPFOR to the downtown objective (0 if none).</param>
    public string? Update(
        float dt,
        int remaining,
        float nearestMeters,
        bool terminalVictory,
        bool terminalDefeat)
    {
        _lineTimer = Math.Max(0, _lineTimer - dt);
        if (_lineTimer <= 0)
            _currentLine = null;

        if (terminalVictory)
            return Push(VictoryLines, "HQ");
        if (terminalDefeat)
            return Push(DefeatLines, "HQ");

        string? announced = null;

        // Fresh wave / new contacts.
        if (remaining > _lastRemaining && remaining > 0 && _lastRemaining >= 0)
            announced = Push(ContactLines, "FO");
        _lastRemaining = remaining;

        // Pressure rises as columns close on Kvadraturen.
        var pressure = ThreatPressure(remaining, nearestMeters);
        if (announced is null && pressure >= 0.55f && _lineTimer <= 0 && _rng.NextDouble() < 0.012f * pressure)
            announced = Push(ContactLines, "FO");

        _idleTimer -= dt;
        if (announced is null && _idleTimer <= 0 && _lineTimer <= 0)
        {
            if (pressure < 0.15f)
            {
                // Quiet sector — rare status chatter only.
                announced = Push(QuietLines, PickSpeaker());
                _idleTimer = 28f + (float)_rng.NextDouble() * 22f;
            }
            else if (pressure < 0.45f)
            {
                announced = Push(
                    ["Maintain observation on the approaches.", "Keep eyes on the harbour road.", "Check fire lanes clear of friendlies."],
                    "FO");
                _idleTimer = 18f + (float)_rng.NextDouble() * 14f;
            }
            else
            {
                announced = Push(ContactLines, "FO");
                _idleTimer = 10f + (float)_rng.NextDouble() * 10f;
            }
        }

        return announced;
    }

    public string AnnounceFire() => Push(FireLines, "GUN ONE");

    public string AnnounceSplash(bool airburst) =>
        Push(airburst ? ["Airburst confirmed.", "Splash, airburst."] : SplashLines, "FO");

    public string AnnounceMissionStart() =>
        Push(["Battery, this is FO — fire mission, danger close.", "Defend the harbour. Engage on my mark."], "FO");

    private static float ThreatPressure(int remaining, float nearestMeters)
    {
        if (remaining <= 0)
            return 0f;

        // 0 at ~4 km, 1 inside ~800 m of the crossing.
        var close = nearestMeters <= 0f
            ? 0.2f
            : Math.Clamp(1f - (nearestMeters - 800f) / 3200f, 0f, 1f);
        var mass = Math.Clamp(remaining / 18f, 0.15f, 1f);
        return Math.Clamp(0.35f * mass + 0.65f * close, 0f, 1f);
    }

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
