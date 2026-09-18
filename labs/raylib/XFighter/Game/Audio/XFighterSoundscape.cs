using Novolis.Audio.Core;
using Novolis.Audio.Effects;
using Novolis.Audio.Playback;

namespace XFighter.Game.Audio;

/// <summary>Engine, music, and combat SFX via Novolis.Audio.Playback + Effects.</summary>
internal sealed class XFighterSoundscape : IDisposable
{
    private readonly IAudioPlayback _sfxPlayback = new NaudioPcmPlayback();
    private readonly IAudioPlayback _musicPlayback = new NaudioPcmPlayback();
    private readonly PcmBuffer _themeLoop = SfxEffectChains.Music.Process(ProceduralSciFiTheme.RenderLoop());
    private CancellationTokenSource? _engineCts;
    private CancellationTokenSource? _musicCts;
    private Task? _engineTask;
    private Task? _musicTask;
    private float _throttle;
    private bool _enabled = true;
    private bool _disposed;

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public void Start()
    {
        if (!_enabled || _engineCts is not null)
            return;

        try
        {
            _engineCts = new CancellationTokenSource();
            _engineTask = Task.Run(() => EngineLoopAsync(_engineCts.Token));
            _musicCts = new CancellationTokenSource();
            _musicTask = Task.Run(() => MusicLoopAsync(_musicCts.Token));
        }
        catch
        {
            _enabled = false;
            StopLoops();
        }
    }

    public void UpdateEngine(float throttle01) =>
        _throttle = Math.Clamp(throttle01, 0f, 1f);

    public void PlayLaser() => PlaySfx(SfxEffectChains.Laser.Process(ProceduralSfx.LaserPew()));

    public void PlayExplosion() => PlaySfx(SfxEffectChains.Explosion.Process(ProceduralSfx.ExplosionBoom()));

    public void PlayRadio() => PlaySfx(SfxEffectChains.Radio.Process(ProceduralSfx.RadioSquelch()));

    public void PlayShieldHit() => PlaySfx(SfxEffectChains.Shield.Process(ProceduralSfx.ShieldHit()));

    public void PlayEnemyBolt() => PlaySfx(SfxEffectChains.EnemyBolt.Process(ProceduralSfx.EnemyBolt()));

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        StopLoops();
        if (_sfxPlayback is IDisposable sfx)
            sfx.Dispose();
        if (_musicPlayback is IDisposable music)
            music.Dispose();
    }

    private async Task EngineLoopAsync(CancellationToken cancellationToken)
    {
        var phase = 0f;
        while (!cancellationToken.IsCancellationRequested && _enabled)
        {
            var buffer = ProceduralSfx.EngineSegment(_throttle, phase);
            phase += (float)buffer.Duration.TotalSeconds;
            try
            {
                await _sfxPlayback.PlayAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                _enabled = false;
                break;
            }
        }
    }

    private async Task MusicLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _enabled)
        {
            try
            {
                await _musicPlayback.PlayAsync(_themeLoop, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                break;
            }
        }
    }

    private void PlaySfx(PcmBuffer buffer)
    {
        if (!_enabled)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await _sfxPlayback.PlayAsync(buffer).ConfigureAwait(false);
            }
            catch
            {
                _enabled = false;
            }
        });
    }

    private void StopLoops()
    {
        _engineCts?.Cancel();
        _musicCts?.Cancel();
        try
        {
            _engineTask?.Wait(TimeSpan.FromSeconds(2));
            _musicTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Best-effort shutdown.
        }

        _engineCts?.Dispose();
        _musicCts?.Dispose();
        _engineCts = null;
        _musicCts = null;
        _engineTask = null;
        _musicTask = null;
    }
}
