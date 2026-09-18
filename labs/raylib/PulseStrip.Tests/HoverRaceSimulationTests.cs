using System.Numerics;
using PulseStrip.Core;
using PulseStrip.Core.Ai;
using Novolis.Simulation.Racing.Tracks;

namespace PulseStrip.Tests;

public class HoverRaceSimulationTests
{
    [Test]
    public async Task Mega_Circuit_Is_About_100x_CompactOval_And_2x_Wide()
    {
        var ovalArc = CenterSplineMath.MeasureArcLength(BuiltInTracks.CompactOval.BuildSpec.CenterLine, 1500);
        var gp = PulseStripCircuits.ByIndex(0);
        var validation = CenterSplineMath.Validate(gp.BuildSpec.CenterLine, 2000);
        await Assert.That(validation.Ok).IsTrue();
        await Assert.That(gp.BuildSpec.TrackHalfWidth).IsEqualTo(8.0);
        await Assert.That(BuiltInTracks.CompactOval.BuildSpec.TrackHalfWidth * 2).IsEqualTo(gp.BuildSpec.TrackHalfWidth);

        var ratio = validation.ArcLength / ovalArc;
        await Assert.That(ratio).IsGreaterThan(80.0);
        await Assert.That(ratio).IsLessThan(130.0);

        // 3D circuit: elevation + side weave must survive bake (not flattened).
        var ySpan = gp.BuildSpec.CenterLine.ControlPoints.Max(p => p.Y)
                    - gp.BuildSpec.CenterLine.ControlPoints.Min(p => p.Y);
        await Assert.That(ySpan).IsGreaterThan(100f);
        var baked = new PulseStripTrackBuilder().Build(gp);
        var bakedY = baked.CenterLineSamples.Max(p => p.Y) - baked.CenterLineSamples.Min(p => p.Y);
        await Assert.That(bakedY).IsGreaterThan(100f);

        var frames = MobiusTrackFrames.Build(baked.CenterLineSamples);
        await Assert.That(frames.Length).IsEqualTo(baked.CenterLineSamples.Count);
        // Möbius half-twist + 2 swirls ⇒ mid-lap up should leave world-up.
        var mid = frames[frames.Length / 2];
        await Assert.That(MathF.Abs(Vector3.Dot(mid.Up, Vector3.UnitY))).IsLessThan(0.98f);
    }

    [Test]
    public async Task PulseStrip_Track_Builder_Stamps_Road_And_Keeps_Invisible_Gates()
    {
        var track = new PulseStripTrackBuilder().Build(PulseStripCircuits.ByIndex(0));
        await Assert.That(track.CenterLineSamples.Count).IsEqualTo(PulseStripTrackBuilder.SampleCount);
        await Assert.That(track.Gates.Count).IsGreaterThan(10);
        await Assert.That(track.Geometry.HalfWidth).IsEqualTo(8.0);
        await Assert.That(track.ProgressMap.TotalArcLength).IsGreaterThan(10_000);
    }

    [Test]
    public async Task Center_Spline_Rejects_Degenerate_Loop()
    {
        var bad = new SplineLoop([new(0, 0, 0), new(1, 0, 0), new(1, 0, 0), new(0, 0, 1)]);
        var result = CenterSplineMath.Validate(bad, 200);
        await Assert.That(result.Ok).IsFalse();
    }

    [Test]
    public async Task Throttle_Moves_Away_From_Start_Along_Track()
    {
        var track = new PulseStripTrackBuilder().Build(PulseStripCircuits.ByIndex(0));
        var player = new PlayerHoverController("T")
        {
            Current = new HoverControlDecision(0, 1, 0, 0, false),
        };
        var sim = new HoverRaceSimulation(track, [player], targetLaps: 1);
        var start = sim.State.Craft[0].Position;
        for (var i = 0; i < 120; i++)
            sim.Tick();

        var c = sim.State.Craft[0];
        var moved = Vector3.Distance(c.Position, start);
        await Assert.That(c.Crashed).IsFalse();
        await Assert.That(c.Speed).IsGreaterThan(20);
        await Assert.That(moved).IsGreaterThan(40f);
        await Assert.That(c.TrackProgress).IsGreaterThan(0.001);
    }

    [Test]
    public async Task Hover_Tick_Advances_Progress_Without_Immediate_Crash()
    {
        var track = new PulseStripTrackBuilder().Build(PulseStripCircuits.ByIndex(0));
        var player = new PlayerHoverController("T")
        {
            Current = new HoverControlDecision(0, 1, 0, 0, false),
        };
        var sim = new HoverRaceSimulation(track, [player], targetLaps: 1);
        for (var i = 0; i < 120; i++)
            sim.Tick();

        await Assert.That(sim.State.Tick).IsEqualTo(120);
        await Assert.That(sim.State.Craft[0].Crashed).IsFalse();
        await Assert.That(sim.State.Craft[0].TrackProgress).IsGreaterThan(0);
    }


    [Test]
    public async Task Weapon_Fire_Consumes_Ammo_And_Spawns_Projectile()
    {
        var track = new PulseStripTrackBuilder().Build(PulseStripCircuits.ByIndex(0));
        var player = new PlayerHoverController("T")
        {
            Current = new HoverControlDecision(0, 0.2, 0, 0, true),
        };
        var sim = new HoverRaceSimulation(track, [player], targetLaps: 1);
        sim.State.Craft[0].WeaponAmmo = 2;
        sim.Tick();
        await Assert.That(sim.State.Craft[0].WeaponAmmo).IsEqualTo(1);
        await Assert.That(sim.State.Projectiles.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Neural_Controller_Clamps_Outputs()
    {
        var net = Novolis.MachineLearning.Neural.DenseNetwork.Create(
            "clamp-test",
            HoverRaceSimulation.SensorInputSize,
            [8],
            HoverRaceSimulation.ControlOutputSize,
            random: new Random(1));
        var controller = new NeuralHoverController(net);
        var track = new PulseStripTrackBuilder().Build(PulseStripCircuits.ByIndex(0));
        var sim = new HoverRaceSimulation(track, [controller], targetLaps: 1);
        for (var i = 0; i < 30; i++)
            sim.Tick();
        await Assert.That(sim.State.Craft[0].Crashed).IsFalse();
    }

    [Test]
    public async Task Evolutionary_Trainer_Produces_Champion()
    {
        var settings = new EvolutionaryHoverTrainerSettings(
            Track: BuiltInTracks.MicroCircle,
            PopulationSize: 6,
            Generations: 2,
            MaxTicksPerEpisode: 200,
            RandomSeed: 7);
        var result = new EvolutionaryHoverTrainer().Train(settings);
        await Assert.That(result.Champion).IsNotNull();
        await Assert.That(result.BestFitnessPerGeneration.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Smoke_Runner_Exits_Zero()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pulsestrip-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var code = PulseStripSmoke.Run(dir);
            await Assert.That(code).IsEqualTo(0);
            await Assert.That(Directory.GetFiles(Path.Combine(dir, "brains"), "*.json").Length).IsGreaterThanOrEqualTo(1);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }
}
