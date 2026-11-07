using Novolis.Audio;

namespace ArtillerySimulator;

/// <summary>SFX, frontline score loop, and battery voice chatter.</summary>
internal sealed class ArtilleryAudioHub : IDisposable
{
    private readonly IAudioEngine _engine;
    private readonly string _assetsDir;
    private readonly ArtillerySfx _sfx;
    private readonly ArtilleryVoice _voice;
    private readonly BatteryChatter _chatter;
    private ISoundHandle? _score;
    private float _scoreReplayIn = -1f;
    private bool _missionAnnounced;
    private bool _terminalAnnounced;
    private bool _started;
    private bool _scoreLoaded;

    private ArtilleryAudioHub(IAudioEngine engine, string assetsDir, Random rng)
    {
        _engine = engine;
        _assetsDir = assetsDir;
        _sfx = new ArtillerySfx(engine, assetsDir);
        _voice = ArtilleryVoice.TryCreate();
        _chatter = new BatteryChatter(rng);
    }

    public string? RadioLine => _chatter.CurrentLine;

    public string? RadioSpeaker => _chatter.CurrentSpeaker;

    public static ArtilleryAudioHub? TryCreate(int seed, bool enabled)
    {
        if (!enabled)
            return null;

        try
        {
            var engine = new Novolis.Audio.Runtime.MiniaudioAudioEngine();
            if (!engine.Start())
            {
                engine.Dispose();
                return null;
            }

            var assets = Path.Combine(AppContext.BaseDirectory, "assets");
            var hub = new ArtilleryAudioHub(engine, assets, new Random(seed ^ 0xA11D10));
            hub._sfx.EnsureReady();
            return hub;
        }
        catch
        {
            return null;
        }
    }

    public void OnEnterPlay()
    {
        if (_started)
            return;
        _started = true;
        EnsureScoreLoaded();
        RestartScore();
        Speak(_chatter.AnnounceMissionStart());
        _sfx.Play("radio-blip");
        _missionAnnounced = true;
    }

    public void OnFire()
    {
        _sfx.Play("muzzle");
        _sfx.Play("breech");
        Speak(_chatter.AnnounceFire());
    }

    public void OnImpact(bool airburst)
    {
        _sfx.PlayImpact(airburst);
        Speak(_chatter.AnnounceSplash(airburst));
    }

    public void Update(float dt, bool playing, bool missionHot, MissionOutcome outcome)
    {
        if (!playing)
            return;

        if (!_missionAnnounced)
            OnEnterPlay();

        if (_scoreReplayIn >= 0f)
        {
            _scoreReplayIn -= dt;
            if (_scoreReplayIn <= 0f)
                RestartScore();
        }

        var terminalVictory = outcome == MissionOutcome.Held;
        var terminalDefeat = outcome is MissionOutcome.Fratricide or MissionOutcome.ObjectiveLost;
        if ((terminalVictory || terminalDefeat) && !_terminalAnnounced)
        {
            _terminalAnnounced = true;
            _sfx.Play("warning");
            var line = _chatter.Update(0f, missionHot, terminalVictory, terminalDefeat);
            if (line is not null)
                Speak(line);
            return;
        }

        var chatter = _chatter.Update(dt, missionHot, terminalVictory: false, terminalDefeat: false);
        if (chatter is not null)
        {
            _sfx.Play("radio-blip");
            Speak(chatter);
        }
    }

    public void Dispose()
    {
        _sfx.Dispose();
        _voice.Dispose();
        _engine.Dispose();
    }

    private void Speak(string line)
    {
        if (_voice.IsAvailable)
            _voice.SpeakComms(line);
    }

    private void EnsureScoreLoaded()
    {
        if (_scoreLoaded)
            return;
        _scoreLoaded = true;
        try
        {
            var path = ArtilleryScoreBake.EnsureBaked(_assetsDir);
            _score = _engine.LoadSound(path);
        }
        catch
        {
            _score = null;
        }
    }

    private void RestartScore()
    {
        if (_score is null)
            return;
        _ = _engine.Play(_score);
        _scoreReplayIn = (float)ArtilleryScoreBake.EstimateDuration().TotalSeconds;
    }
}
