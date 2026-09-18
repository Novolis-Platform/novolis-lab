using KatoriLab.Demo;

namespace KatoriLab.Tests;

public sealed class KenTimelineTests
{
    [Test]
    public async Task Duration_covers_full_dojo_kata()
    {
        var duration = KenTimeline.Duration;
        await Assert.That(duration).IsGreaterThanOrEqualTo(30f);
    }

    [Test]
    public async Task Phase_seeks_are_ordered()
    {
        float prev = -1f;
        foreach (var (id, time, _) in KenTimeline.Phases)
        {
            await Assert.That(time).IsGreaterThan(prev);
            await Assert.That(KenTimeline.TimeForPhase(id)).IsEqualTo(time);
            prev = time;
        }
    }

    [Test]
    public async Task TipDir_is_unit_length_across_timeline()
    {
        for (var t = 0f; t <= KenTimeline.Duration; t += 0.25f)
        {
            var err = MathF.Abs(KenTimeline.Evaluate(t).TipDir.Length() - 1f);
            await Assert.That(err).IsLessThan(0.02f);
        }
    }

    [Test]
    public async Task TipDir_does_not_snap_outside_cut()
    {
        var prev = KenTimeline.Evaluate(0f).TipDir;
        for (var t = 1f / 30f; t < KenTimeline.Duration; t += 1f / 30f)
        {
            var sample = KenTimeline.Evaluate(t);
            var cutting = sample.Label.Contains("Kesagiri", StringComparison.Ordinal)
                          || sample.Label.Contains("Cutting", StringComparison.Ordinal);
            var dot = System.Numerics.Vector3.Dot(
                System.Numerics.Vector3.Normalize(prev),
                System.Numerics.Vector3.Normalize(sample.TipDir));
            if (!cutting)
                await Assert.That(dot).IsGreaterThanOrEqualTo(0.55f);
            prev = sample.TipDir;
        }
    }

    [Test]
    public async Task Door_and_center_constants_match_spatial_story()
    {
        var span = KenTimeline.CenterZ - KenTimeline.DoorZ;
        await Assert.That(span).IsGreaterThan(1.5f);
        var door = KenTimeline.Evaluate(KenTimeline.TimeForPhase("door"));
        var opening = KenTimeline.Evaluate(KenTimeline.TimeForPhase("opening"));
        var travel = opening.RootOffset.Z - door.RootOffset.Z;
        await Assert.That(travel).IsGreaterThanOrEqualTo(1.5f);
    }
}
