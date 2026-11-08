using System.Numerics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>
/// Fixed-step battle integration: met, crew, ballistics, mission impacts.
/// Presentation FX (particles/audio/killcam/aim) advance separately via
/// <see cref="AdvancePresentation"/> so render cost cannot multiply physics work.
/// </summary>
internal sealed class ArtilleryBattleLoop
{
    public const float KillcamReturnSeconds = 3f;

    private readonly TerrainWorld _terrain;
    private readonly AtmosphereModel _atmosphere;
    private readonly ArtilleryRoundPool _rounds;
    private readonly AimPreviewService _aim;
    private readonly HashSet<BallisticTrajectoryRunner> _impactHandled = [];
    private readonly BattleFxSink _fx;
    private readonly ILogger _log;

    private GunModel _gun = null!;
    private CrewDrill _crew = null!;
    private ProjectileBallisticSimulation _ballistics = null!;
    private IDefenseMission? _mission;

    public ArtilleryBattleLoop(
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        ArtilleryRoundPool rounds,
        AimPreviewService aim,
        BattleFxSink fx,
        ILogger? log = null)
    {
        _terrain = terrain;
        _atmosphere = atmosphere;
        _rounds = rounds;
        _aim = aim;
        _fx = fx;
        _log = log ?? NullLogger.Instance;
    }

    public HashSet<BallisticTrajectoryRunner> ImpactHandled => _impactHandled;
    public float BurstAge { get; private set; } = -1f;
    public Vector3 BurstAt { get; private set; }
    public float MuzzleFlash { get; private set; }
    public bool KillcamActive { get; set; }
    public AimPreviewService Aim => _aim;

    public void Bind(GunModel gun, CrewDrill crew, ProjectileBallisticSimulation ballistics)
    {
        _gun = gun;
        _crew = crew;
        _ballistics = ballistics;
    }

    public void SetMission(IDefenseMission? mission) => _mission = mission;

    public void ResetVisuals()
    {
        BurstAge = -1f;
        MuzzleFlash = 0f;
        KillcamActive = false;
    }

    public void NotifyFired(Vector3 muzzle, Vector3 fireDir)
    {
        MuzzleFlash = 0.22f;
        _fx.OnMuzzle(muzzle, fireDir);
        _log.LogDebug("Muzzle flash at {Muzzle}", muzzle);
    }

    /// <summary>Physics only — fixed-step ballistics / mission. No aim rebuild, no particle age.</summary>
    public void AdvancePhysics(int steps, float battleSeconds)
    {
        if (steps <= 0)
            return;

        _atmosphere.Advance(battleSeconds);
        if (_atmosphere.ConsumeAimInvalidation())
            _aim.MarkDirty();
        _crew.Advance(_gun, battleSeconds);

        Vector3? incoming = null;
        foreach (var round in _rounds.Rounds)
        {
            if (round.Phase != BallisticTrajectoryPhase.InFlight)
                continue;

            for (var i = 0; i < steps && round.Phase == BallisticTrajectoryPhase.InFlight; i++)
            {
                var env = _atmosphere.BallisticEnvironment(round.CurrentPosition.Y);
                round.AdvanceOne(_ballistics, env, _terrain.Collision, _terrain.Field, _terrain.GunBaseline);
            }

            if (round.Phase == BallisticTrajectoryPhase.InFlight)
                incoming ??= round.CurrentPosition;
        }

        _mission?.Advance(battleSeconds, incoming);

        foreach (var round in _rounds.Rounds)
        {
            if (round.Phase != BallisticTrajectoryPhase.Impacted
                || round.Impact is not { } landed
                || !_impactHandled.Add(round))
                continue;

            BurstAge = 0f;
            BurstAt = BurstPoint(landed.Position);
            var airburst = _gun.Spec.Warhead.IsAirburst;
            _fx.OnBurst(BurstAt, airburst);
            if (_mission is { } mission)
            {
                mission.ResolveImpact(landed.Position);
                _fx.OnImpactScored(mission.LastBracket, mission.LastPinned);
            }

            _log.LogDebug(
                "Impact {Reason} at {Position} airburst={Airburst}",
                landed.Reason,
                BurstAt,
                airburst);
        }
    }

    /// <summary>Render-rate FX: muzzle/burst age, killcam, particles/audio, aim arcs (once).</summary>
    public void AdvancePresentation(float wallSeconds, bool audioPlaying)
    {
        if (BurstAge >= 0f)
        {
            BurstAge += wallSeconds;
            if (KillcamActive && BurstAge >= KillcamReturnSeconds)
            {
                KillcamActive = false;
                _fx.OnKillcamExpired();
            }
        }

        if (MuzzleFlash > 0f)
            MuzzleFlash = MathF.Max(0f, MuzzleFlash - wallSeconds);

        _fx.OnFrameFx(wallSeconds, audioPlaying, _mission);
        _aim.RebuildIfNeeded(_crew, wallSeconds);
    }

    public Vector3 BurstPoint(Vector3 impact)
    {
        var warhead = _gun.Spec.Warhead;
        var ground = _terrain.Field.SampleHeight(impact.X, impact.Z);
        var height = warhead.IsAirburst ? warhead.AirburstHeightMeters : MathF.Max(2f, impact.Y - ground);
        return new Vector3(impact.X, ground + height, impact.Z);
    }
}
