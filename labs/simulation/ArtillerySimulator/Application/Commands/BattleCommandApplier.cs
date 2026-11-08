using System.Globalization;
using Microsoft.Extensions.Logging;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

internal sealed class BattleCommandApplier
{
    private readonly TerrainWorld _terrain;
    private readonly BattleSession _session;
    private readonly CrewDrill _crew;
    private readonly ArtilleryRoundPool _rounds;
    private readonly ArtilleryBattleLoop _battle;
    private readonly AimPreviewService _aim;
    private readonly ArtilleryCameras _camera;
    private readonly FireAuthorizationRules _fireRules;
    private readonly Func<GunModel> _gun;
    private readonly Func<bool> _isPaused;
    private readonly Func<BattleCommandResult> _cycleGun;
    private readonly ScoreBoard _score;
    private readonly ILogger _log;

    public BattleCommandApplier(
        TerrainWorld terrain,
        BattleSession session,
        CrewDrill crew,
        ArtilleryRoundPool rounds,
        ArtilleryBattleLoop battle,
        AimPreviewService aim,
        ArtilleryCameras camera,
        FireAuthorizationRules fireRules,
        Func<GunModel> gun,
        Func<bool> isPaused,
        Func<BattleCommandResult> cycleGun,
        ScoreBoard score,
        ILogger logger)
    {
        _terrain = terrain;
        _session = session;
        _crew = crew;
        _rounds = rounds;
        _battle = battle;
        _aim = aim;
        _camera = camera;
        _fireRules = fireRules;
        _gun = gun;
        _isPaused = isPaused;
        _cycleGun = cycleGun;
        _score = score;
        _log = logger;
    }

    public BattleCommandResult Apply(BattleCommand command) =>
        command.Kind switch
        {
            BattleCommandKind.Aim => ApplyAim(command),
            BattleCommandKind.Charge => ApplyCharge(command),
            BattleCommandKind.Fire => ApplyFire(),
            BattleCommandKind.FollowCamera => ApplyFollow(command.FollowShell),
            BattleCommandKind.CycleGun => _cycleGun(),
            _ => BattleCommandResult.Fail("unavailable", "Unknown battle command."),
        };

    private BattleCommandResult ApplyAim(BattleCommand command)
    {
        if (_isPaused())
            return BattleCommandResult.Fail("paused", "Battle clock paused — resume (Esc) before commanding the gun.");

        var gun = _gun();
        var elev = Math.Clamp(command.ElevationDegrees, gun.Spec.MinElevationDegrees, gun.Spec.MaxElevationDegrees);
        _crew.OrderAim(gun, elev, command.AzimuthDegrees);
        _aim.Rebuild(_crew);
        var message = _crew.Ready(gun)
            ? string.Create(CultureInfo.InvariantCulture, $"On lay at {_crew.OrderedElevation:F1}° elevation, {_crew.OrderedAzimuth:F1}° azimuth.")
            : string.Create(CultureInfo.InvariantCulture, $"Laying to {_crew.OrderedElevation:F1}° / {_crew.OrderedAzimuth:F1}°. Tube at {gun.ElevationDegrees:F1}° / {gun.AzimuthDegrees:F1}°.");
        return BattleCommandResult.Ok(message);
    }

    private BattleCommandResult ApplyCharge(BattleCommand command)
    {
        if (_isPaused())
            return BattleCommandResult.Fail("paused", "Battle clock paused — resume (Esc) before commanding the gun.");

        var gun = _gun();
        var index = command.ChargeIndex;
        if (index < 0 || index >= gun.Spec.Charges.Count)
            return BattleCommandResult.Fail("invalid-arguments", $"Charge index must be 0..{gun.Spec.Charges.Count - 1}.");

        _crew.OrderCharge(gun, index);
        _aim.Rebuild(_crew);
        var message = _crew.Ready(gun)
            ? string.Create(CultureInfo.InvariantCulture, $"Charge {gun.ChargeLabel} ({gun.MuzzleSpeedMps:F0} m/s).")
            : string.Create(CultureInfo.InvariantCulture, $"Charge {gun.ChargeLabel} ({gun.MuzzleSpeedMps:F0} m/s). {_crew.Phrase(gun)}");
        return BattleCommandResult.Ok(message);
    }

    private BattleCommandResult ApplyFire()
    {
        if (_isPaused())
            return BattleCommandResult.Fail("paused", "Battle clock paused — resume (Esc) before commanding the gun.");

        if (_fireRules.FireBlockReason() is { } blocked)
            return BattleCommandResult.Fail("unavailable", blocked);

        var gun = _gun();
        _crew.SnapTube(gun);
        var muzzle = gun.MuzzlePosition(_terrain.GunBaseline);
        var salvo = Math.Clamp(gun.Spec.SalvoRoundCount, 1, ArtilleryRoundPool.MaxLiveRounds);
        var spread = gun.Spec.SalvoSpreadMeters;
        BallisticTrajectoryRunner? lead = null;

        for (var i = 0; i < salvo; i++)
        {
            var elev = gun.ElevationDegrees;
            var az = gun.AzimuthDegrees;
            if (salvo > 1 && spread > 1f)
            {
                // Small sheaf around the ordered MPI — educational GMLRS carpet, not a fire-control computer.
                var t = salvo == 1 ? 0f : (i / (float)(salvo - 1)) - 0.5f;
                var ring = spread * (0.35f + 0.65f * MathF.Abs(t));
                var bearing = (i * 47.2f) * (MathF.PI / 180f);
                var dAz = (ring / 1_000f) * MathF.Cos(bearing) * (180f / MathF.PI);
                var dEl = (ring / 1_800f) * MathF.Sin(bearing) * (180f / MathF.PI);
                elev = Math.Clamp(elev + dEl, gun.Spec.MinElevationDegrees, gun.Spec.MaxElevationDegrees);
                az += dAz;
            }

            var delivery = _session.Delivery.SampleFire(elev, az, gun.MuzzleSpeedMps);
            var fireDir = GunModel.DirectionOf(delivery.ElevationDegrees, delivery.AzimuthDegrees);
            var follow = i == 0 && _camera.FollowProjectile;
            var round = _rounds.Acquire(follow, _battle.ImpactHandled);
            round.Begin(gun.CreateProjectileState(muzzle, fireDir, delivery.MuzzleSpeedMps));
            _battle.ImpactHandled.Remove(round);
            if (i == 0)
            {
                lead = round;
                _rounds.Active = round;
                _battle.NotifyFired(muzzle, fireDir);
            }
        }

        _crew.BeginReload(gun);
        _battle.KillcamActive = _camera.FollowProjectile;
        _aim.MarkDirty();
        _score.RegisterShot();
        _log.LogDebug(
            "Fired {Charge} x{Salvo} at elev={Elev:F1} az={Az:F1}",
            gun.ChargeLabel,
            salvo,
            gun.ElevationDegrees,
            gun.AzimuthDegrees);

        var msg = salvo > 1
            ? string.Create(CultureInfo.InvariantCulture, $"Salvo {salvo} away (GMLRS sheaf).")
            : "Round away. Impact will arrive as artillery.impact.";
        return BattleCommandResult.Ok(msg, lead);
    }

    private BattleCommandResult ApplyFollow(bool followShell)
    {
        _camera.FollowProjectile = followShell;
        _battle.KillcamActive = followShell;
        return BattleCommandResult.Ok(followShell ? "Camera follows the shell." : "Camera follows the gun.");
    }
}
