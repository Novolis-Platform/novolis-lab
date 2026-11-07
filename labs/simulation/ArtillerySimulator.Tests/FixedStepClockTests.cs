namespace ArtillerySimulator.Tests;

public sealed class FixedStepClockTests
{
    [Test]
    public async Task Consume_at_60fps_yields_two_steps_per_frame()
    {
        var clock = new FixedStepClock();
        var steps = clock.Consume(1f / 60f);
        await Assert.That(steps).IsEqualTo(2);
        await Assert.That(clock.AccumulatorSeconds).IsLessThan(FixedStepClock.DtSeconds);
    }

    [Test]
    public async Task Consume_at_240fps_accumulates_until_a_step()
    {
        var clock = new FixedStepClock();
        var total = 0;
        for (var i = 0; i < 8; i++)
            total += clock.Consume(1f / 240f);

        // 8/240 = 1/30 ≈ 4 steps at 120 Hz
        await Assert.That(total).IsEqualTo(4);
    }

    [Test]
    public async Task Consume_caps_steps_and_keeps_remainder_bounded()
    {
        var clock = new FixedStepClock();
        var steps = clock.Consume(2f, maxSteps: 32);
        await Assert.That(steps).IsEqualTo(32);
        await Assert.That(clock.AccumulatorSeconds).IsLessThanOrEqualTo(FixedStepClock.DtSeconds * 32);
    }

    [Test]
    public async Task Same_wall_time_same_total_steps_across_fps()
    {
        static int Run(float fps)
        {
            var clock = new FixedStepClock();
            var wall = 1f;
            var dt = 1f / fps;
            var steps = 0;
            for (var t = 0f; t < wall - 1e-6f; t += dt)
                steps += clock.Consume(dt);
            return steps;
        }

        var at60 = Run(60f);
        var at144 = Run(144f);
        // One second of wall ≈ 120 fixed steps (within catch-up / remainder noise).
        await Assert.That(at60).IsBetween(118, 122);
        await Assert.That(at144).IsBetween(118, 122);
        await Assert.That(Math.Abs(at60 - at144)).IsLessThanOrEqualTo(2);
    }
}
