using Microsoft.Extensions.Logging;
using Novolis.Physics.Ballistics;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>
/// Headless exit-code smoke: load world → aim → fire → wait for impact.
/// Driven from the Silk update loop (no separate HTTP process required).
/// </summary>
internal sealed class ArtillerySmokeDriver
{
    private readonly BattleWorld _world;
    private readonly ILogger<ArtillerySmokeDriver> _log;
    private Phase _phase = Phase.WaitWorld;
    private float _wait;
    private int _exitCode = 1;
    private bool _done;

    public ArtillerySmokeDriver(BattleWorld world, ILogger<ArtillerySmokeDriver> log)
    {
        _world = world;
        _log = log;
    }

    public bool IsDone => _done;
    public int ExitCode => _exitCode;

    public void Tick(SilkFrame frame)
    {
        if (_done)
            return;

        _wait += frame.DeltaSeconds;
        switch (_phase)
        {
            case Phase.WaitWorld:
                if (!_world.WorldReady)
                {
                    if (_wait > 120f)
                        Fail(frame, "World load timed out.");
                    return;
                }

                _log.LogInformation("Smoke: world ready — ordering aim/charge");
                _ = _world.Apply(BattleCommand.Charge(1));
                _ = _world.Apply(BattleCommand.Aim(45f, _world.Gun.AzimuthDegrees));
                _phase = Phase.WaitCrew;
                _wait = 0f;
                break;

            case Phase.WaitCrew:
                if (_world.FireBlockReason() is not null && _wait < 45f)
                    return;
                if (_world.FireBlockReason() is { } blocked)
                {
                    Fail(frame, $"Could not fire: {blocked}");
                    return;
                }

                _log.LogInformation("Smoke: firing");
                var fired = _world.Apply(BattleCommand.Fire());
                if (!fired.Succeeded)
                {
                    Fail(frame, fired.Message);
                    return;
                }

                _phase = Phase.WaitImpact;
                _wait = 0f;
                break;

            case Phase.WaitImpact:
                if (_world.Shot.Phase == BallisticTrajectoryPhase.Impacted
                    || _world.Shot.Impact is not null)
                {
                    _log.LogInformation(
                        "Smoke OK: impact phase={Phase} range={Range:F0}m",
                        _world.Shot.Phase,
                        _world.Shot.Impact?.HorizontalRangeMeters ?? 0f);
                    _exitCode = 0;
                    Done(frame);
                    return;
                }

                if (_wait > 90f)
                    Fail(frame, "Impact timed out.");
                break;
        }
    }

    private void Fail(SilkFrame frame, string message)
    {
        _log.LogError("Smoke FAILED: {Message}", message);
        Console.Error.WriteLine($"SMOKE FAILED: {message}");
        _exitCode = 1;
        Done(frame);
    }

    private void Done(SilkFrame frame)
    {
        _done = true;
        frame.Close();
    }

    private enum Phase : byte
    {
        WaitWorld,
        WaitCrew,
        WaitImpact,
    }
}
