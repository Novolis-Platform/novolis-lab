using System.Threading.Channels;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>
/// Silk → intents. Discrete fire/charge/follow go through <see cref="BattleCommand"/> channel;
/// continuous WASD lay stays sync via <see cref="IBatteryApplier.NudgeOrder"/>.
/// </summary>
internal sealed class PlayerBattleInput
{
    private const float FineMilsPerSecond = 12f;
    private const float LayRampSeconds = 0.4f;

    private readonly ChannelWriter<BattleCommand> _commands;
    private float _layHoldSeconds;

    public PlayerBattleInput(ChannelWriter<BattleCommand> commands)
    {
        _commands = commands;
    }

    public void Reset() => _layHoldSeconds = 0f;

    public void Handle(
        SilkFrame frame,
        float battleSeconds,
        bool paused,
        bool missionTerminal,
        IBatteryApplier battery,
        FireControlUi ui)
    {
        if (paused)
            return;

        var actions = ui.HandleMouse(frame);
        if (actions.ChargeIndex is { } charge)
            _ = _commands.TryWrite(BattleCommand.Charge(charge));

        if (!missionTerminal && (actions.Fire || frame.IsKeyPressed(Key.Space)))
            _ = _commands.TryWrite(BattleCommand.Fire());

        var ctrl = frame.IsKeyDown(Key.ControlLeft) || frame.IsKeyDown(Key.ControlRight);
        if (actions.ToggleFollow || frame.IsKeyPressed(Key.F) || (frame.IsKeyPressed(Key.C) && !ctrl))
            battery.ToggleOptionalShellFollow();

        if (actions.SnapGun)
            battery.SnapCameraToGun();

        if (actions.SnapBattle)
            battery.SnapCameraToBattle();

        if (actions.MapOverview)
            battery.ShowMapOverview();

        if (missionTerminal)
            return;

        var fine = frame.IsKeyDown(Key.ShiftLeft) || frame.IsKeyDown(Key.ShiftRight);
        var laying = frame.IsKeyDown(Key.W) || frame.IsKeyDown(Key.Up)
            || frame.IsKeyDown(Key.S) || frame.IsKeyDown(Key.Down)
            || frame.IsKeyDown(Key.A) || frame.IsKeyDown(Key.Left)
            || frame.IsKeyDown(Key.D) || frame.IsKeyDown(Key.Right);

        if (laying)
            _layHoldSeconds += MathF.Max(1e-4f, frame.DeltaSeconds);
        else
            _layHoldSeconds = 0f;

        var ramp = Math.Clamp(_layHoldSeconds / LayRampSeconds, 0.2f, 1f);
        var elevate = (fine ? FineMilsPerSecond : CrewDrill.ElevateMilsPerSecond) / NatoMils.MilsPerDegree * battleSeconds * ramp;
        var traverse = (fine ? FineMilsPerSecond : CrewDrill.TraverseMilsPerSecond) / NatoMils.MilsPerDegree * battleSeconds * ramp;

        var dElev = 0f;
        var dTrav = 0f;
        if (frame.IsKeyDown(Key.W) || frame.IsKeyDown(Key.Up))
            dElev += elevate;
        if (frame.IsKeyDown(Key.S) || frame.IsKeyDown(Key.Down))
            dElev -= elevate;
        if (frame.IsKeyDown(Key.A) || frame.IsKeyDown(Key.Left))
            dTrav += traverse;
        if (frame.IsKeyDown(Key.D) || frame.IsKeyDown(Key.Right))
            dTrav -= traverse;

        if (dElev != 0f || dTrav != 0f)
            battery.NudgeOrder(dElev, dTrav);
    }

    public void ToggleOptionalShellFollow(
        ArtilleryCameras camera,
        ArtilleryBattleLoop battle,
        ArtilleryRoundPool rounds,
        TerrainWorld terrain,
        GunModel gun)
    {
        var round = rounds.RoundForCamera(battle.BurstAge, ArtilleryBattleLoop.KillcamReturnSeconds);
        var roundLive = round is not null
            && (round.Phase is Novolis.Physics.Ballistics.BallisticTrajectoryPhase.InFlight
                || (round.Phase == Novolis.Physics.Ballistics.BallisticTrajectoryPhase.Impacted
                    && battle.BurstAge >= 0f
                    && battle.BurstAge < ArtilleryBattleLoop.KillcamReturnSeconds));

        if (!roundLive)
        {
            camera.FollowProjectile = false;
            battle.KillcamActive = false;
            return;
        }

        rounds.Active = round!;
        camera.ToggleFollowTarget();
        battle.KillcamActive = camera.FollowProjectile;
        if (!battle.KillcamActive)
            camera.SnapToGun(terrain.GunBaseline, gun.BarrelDirection());
    }
}
