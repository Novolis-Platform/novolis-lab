namespace PulseStrip.Core;

using Novolis.Simulation.Racing.Tracks;
using PulseStrip.Core.Ai;

/// <summary>Headless smoke: train (or load) brains and tick a short race.</summary>
public static class PulseStripSmoke
{
    public static int Run(string? contentRoot = null, CancellationToken cancellationToken = default)
    {
        contentRoot ??= Path.Combine(AppContext.BaseDirectory, "Content");
        var brainsDir = Path.Combine(contentRoot, "brains");

        var brains = BrainStore.LoadOrTrain(
            brainsDir,
            count: 3,
            trainTrack: BuiltInTracks.MicroCircle,
            forceRetrain: false,
            cancellationToken);

        var track = new PulseStripTrackBuilder().Build(PulseStripCircuits.ByIndex(0));
        var player = new PlayerHoverController("SmokePilot")
        {
            Current = new HoverControlDecision(0, 1, 0, 0, false),
        };
        var controllers = new List<IHoverController> { player };
        foreach (var brain in brains)
            controllers.Add(new NeuralHoverController(brain));

        var sim = new HoverRaceSimulation(track, controllers, targetLaps: 1);
        const int maxTicks = 1800;
        for (var t = 0; t < maxTicks; t++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Mild steering so smoke makes progress without a window.
            player.Current = new HoverControlDecision(
                Steering: Math.Sin(t * 0.01) * 0.15,
                Throttle: 1.0,
                Brake: 0,
                Boost: t % 240 < 40 ? 1.0 : 0.0,
                Fire: t % 90 == 0);
            sim.Tick();
            if (sim.State.RaceFinished || sim.State.Craft[0].Finished || sim.State.Craft[0].Crashed)
                break;
        }

        Console.WriteLine(
            $"PulseStrip smoke OK — ticks={sim.State.Tick} progress={sim.State.Craft[0].TrackProgress:F2} place={sim.State.Craft[0].Place}");
        return 0;
    }
}
