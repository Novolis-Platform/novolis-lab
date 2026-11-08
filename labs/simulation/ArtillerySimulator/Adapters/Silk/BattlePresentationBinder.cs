using Microsoft.Extensions.Logging;

namespace ArtillerySimulator;

/// <summary>
/// Wires <see cref="BattleFxSink"/> to particles / audio / camera.
/// Keeps presentation adapters out of <see cref="BattleWorld"/> construction.
/// </summary>
internal sealed class BattlePresentationBinder
{
    private readonly BattleFxSink _fx;
    private readonly ArtilleryParticles _particles;
    private readonly ArtilleryCameras _camera;
    private readonly ILogger<BattlePresentationBinder> _log;
    private BattleWorld? _world;
    private ArtilleryAudioHub? _audio;
    private float _fxTime;
    private bool _bound;

    public BattlePresentationBinder(
        BattleFxSink fx,
        ArtilleryParticles particles,
        ArtilleryCameras camera,
        ILogger<BattlePresentationBinder> log)
    {
        _fx = fx;
        _particles = particles;
        _camera = camera;
        _log = log;
    }

    public float FxTime => _fxTime;
    public ArtilleryAudioHub? Audio => _audio;
    public ArtilleryParticles Particles => _particles;

    public void Bind(BattleWorld world, ArtilleryAudioHub? audio)
    {
        if (_bound)
            return;

        _world = world;
        _audio = audio;
        _bound = true;

        _fx.Muzzle = (muzzle, dir) =>
        {
            _particles.SpawnMuzzle(muzzle, dir);
            _audio?.OnFire();
        };
        _fx.Burst = (at, airburst) =>
        {
            _particles.SpawnBurst(at, airburst);
            _audio?.OnImpact(airburst);
        };
        _fx.KillcamExpired = () =>
        {
            if (_world is null)
                return;
            _camera.FollowProjectile = false;
            _camera.SnapToGun(_world.Terrain.GunBaseline, _world.Gun.BarrelDirection());
        };
        _fx.FrameFx = (wall, playing, mission) =>
        {
            _particles.Update(wall);
            _fxTime += wall;
            if (!playing)
                return;

            _audio?.Update(
                wall,
                playing: true,
                remaining: mission?.Remaining ?? 0,
                nearestMeters: mission?.NearestMeters ?? 0f,
                mission?.Outcome ?? MissionOutcome.Open);
        };

        _log.LogDebug("Battle presentation FX bound (audio={HasAudio})", _audio is not null);
    }
}
