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
        _log = logger;
    }

    public BattleCommandResult Apply(BattleCommand command) =>
        command.Kind switch
        {
            BattleCommandKind.Aim => ApplyAim(command),
            BattleCommandKind.Charge => ApplyCharge(command),
            BattleCommandKind.Fire => ApplyFire(),
            BattleCommandKind.FollowCamera => ApplyFollow(command.FollowShell),
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
        var delivery = _session.Delivery.SampleFire(gun.ElevationDegrees, gun.AzimuthDegrees, gun.MuzzleSpeedMps);
        var fireDir = GunModel.DirectionOf(delivery.ElevationDegrees, delivery.AzimuthDegrees);
        _rounds.Active = _rounds.Acquire(_camera.FollowProjectile, _battle.ImpactHandled);
        _rounds.Active.Begin(gun.CreateProjectileState(muzzle, fireDir, delivery.MuzzleSpeedMps));
        _battle.ImpactHandled.Remove(_rounds.Active);
        _battle.NotifyFired(muzzle, fireDir);
        _crew.BeginReload();
        _battle.KillcamActive = _camera.FollowProjectile;
        _aim.MarkDirty();
        _log.LogDebug(
            "Fired {Charge} at elev={Elev:F1} az={Az:F1}",
            gun.ChargeLabel,
            gun.ElevationDegrees,
            gun.AzimuthDegrees);
        return BattleCommandResult.Ok("Round away. Impact will arrive as artillery.impact.", _rounds.Active);
    }

    private BattleCommandResult ApplyFollow(bool followShell)
    {
        _camera.FollowProjectile = followShell;
        _battle.KillcamActive = followShell;
        return BattleCommandResult.Ok(followShell ? "Camera follows the shell." : "Camera follows the gun.");
    }
}
