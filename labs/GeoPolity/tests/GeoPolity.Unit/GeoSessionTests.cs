using GeoPolity.Agent;
using GeoPolity.Session;
using Novolis.Agent.Core;
using Novolis.Geopolitics.Core;
using Novolis.Geopolitics.Scenarios;

namespace GeoPolity.Unit;

public sealed class GeoSessionTests
{
    [Test]
    public async Task SessionClock_SpeedPresets_SetDaysAndResume()
    {
        var clock = new SessionClockController();
        await Assert.That(clock.Running).IsFalse();

        clock.SetSpeedPreset(1);
        await Assert.That(clock.DaysPerPulse).IsEqualTo(1);
        await Assert.That(clock.Running).IsTrue();

        clock.SetSpeedPreset(3);
        await Assert.That(clock.DaysPerPulse).IsEqualTo(WorldState.DaysPerMonth);

        clock.SetSpeedPreset(5);
        await Assert.That(clock.DaysPerPulse).IsEqualTo(WorldState.DaysPerYear * 5);

        clock.Pause();
        await Assert.That(clock.ShouldPulse).IsFalse();
        clock.ModalBlocks = true;
        clock.Resume();
        await Assert.That(clock.ShouldPulse).IsFalse();
    }

    [Test]
    public async Task GeoSessionCommands_Step_AdvancesDay()
    {
        var session = new GeoSession(ProceduralWorldGenerator.Generate(21), rngSeed: 21);
        var day0 = session.World.Day;
        GeoSessionCommands.Step(session, 7);
        await Assert.That(session.World.Day).IsEqualTo(day0 + 7);
        await Assert.That(session.Clock.Running).IsFalse();
    }

    [Test]
    public async Task AgentHost_Execute_Step_AdvancesWorld()
    {
        var session = new GeoSession(ProceduralWorldGenerator.Generate(22), rngSeed: 22);
        var host = new GeoPolityAgentHost(session);
        var day0 = session.World.Day;

        var result = host.Execute(new AgentCommand
        {
            ActionId = GeoPolityActionIds.Step,
            Params = { ["days"] = "3" },
        });

        await Assert.That(result.Ok).IsTrue();
        await Assert.That(session.World.Day).IsEqualTo(day0 + 3);
        await Assert.That(host.Snapshot().StatusLines["day"]).IsEqualTo((day0 + 3).ToString());
    }

    [Test]
    public async Task AgentHost_PauseResume_ToggleRunning()
    {
        var session = new GeoSession(ProceduralWorldGenerator.Generate(23), rngSeed: 23);
        var host = new GeoPolityAgentHost(session);
        GeoSessionCommands.Resume(session);

        var paused = host.Execute(new AgentCommand { ActionId = GeoPolityActionIds.Pause });
        await Assert.That(paused.Ok).IsTrue();
        await Assert.That(session.Clock.Running).IsFalse();

        var resumed = host.Execute(new AgentCommand { ActionId = GeoPolityActionIds.Resume });
        await Assert.That(resumed.Ok).IsTrue();
        await Assert.That(session.Clock.Running).IsTrue();
    }
}
