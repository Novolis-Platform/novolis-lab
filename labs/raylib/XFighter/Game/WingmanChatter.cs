namespace XFighter.Game;

/// <summary>Rebel-style comms lines and timing (original dialogue, SW-inspired tone).</summary>
internal sealed class WingmanChatter
{
    private static readonly string[] IdleLines =
    [
        "Red Five standing by.",
        "S-foils locked in attack position.",
        "Red Two, copy — maintain formation.",
        "All craft, report in.",
        "Stay on target. Stay on target.",
        "We've got incoming H-fighters — heads up.",
        "Accelerate to attack speed.",
        "Look at the size of that thing!",
        "Red Leader, going in.",
        "Almost there… almost there…",
    ];

    private static readonly string[] KillLines =
    [
        "Great shot, Red Five!",
        "Direct hit!",
        "He's on fire — nice shooting.",
        "Scratch one H-fighter.",
        "Yeah, that got him!",
    ];

    private static readonly string[] DangerLines =
    [
        "I can't shake him!",
        "Red Five, enemies on your six!",
        "Deflector stress critical — pull out!",
        "They're all over me!",
        "Watch it — you've got one on your tail!",
    ];

    private readonly Random _rng;
    private float _idleTimer = 8f;
    private string? _currentLine;
    private float _lineTimer;
    private string? _speaker = "RED TWO";

    public WingmanChatter(Random rng) => _rng = rng;

    public string? CurrentLine => _lineTimer > 0 ? _currentLine : null;

    public string? CurrentSpeaker => _lineTimer > 0 ? _speaker : null;

    public float LineAlpha => _lineTimer <= 0 ? 0f : Math.Clamp(_lineTimer / 0.35f, 0f, 1f);

    public void Reset()
    {
        _idleTimer = 4f;
        _lineTimer = 0;
        _currentLine = null;
    }

    public string? Update(float dt, int activeEnemies, float shield, int killsThisFrame)
    {
        _lineTimer = Math.Max(0, _lineTimer - dt);
        if (_lineTimer <= 0)
            _currentLine = null;

        string? announcedLine = null;
        if (killsThisFrame > 0 && _rng.NextDouble() < 0.55)
            announcedLine = Push(KillLines, "RED THREE");
        else if (shield < 0.25f && _rng.NextDouble() < 0.02)
            announcedLine = Push(DangerLines, "RED TWO");
        else if (activeEnemies >= 3 && _rng.NextDouble() < 0.006)
            announcedLine = Push(DangerLines, "RED LEADER");

        _idleTimer -= dt;
        if (announcedLine is null && _idleTimer <= 0 && _lineTimer <= 0)
        {
            announcedLine = Push(IdleLines, PickSpeaker());
            _idleTimer = 14f + (float)_rng.NextDouble() * 16f;
        }

        return announcedLine;
    }

    public string AnnounceWave() => Push(
        ["Red squadron, all fighters launch.", "Red Leader, this is Red Five — beginning our attack run."],
        "RED LEADER");

    private string Push(string[] pool, string speaker)
    {
        _currentLine = pool[_rng.Next(pool.Length)];
        _speaker = speaker;
        _lineTimer = 5.5f;
        return _currentLine;
    }

    private string PickSpeaker() =>
        _rng.Next(4) switch
        {
            0 => "RED LEADER",
            1 => "RED TWO",
            2 => "RED THREE",
            _ => "BASE",
        };
}
