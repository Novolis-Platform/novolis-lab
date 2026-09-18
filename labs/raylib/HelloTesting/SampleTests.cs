using Novolis.Raylib.Testing;

namespace HelloTesting;

public class SampleTests
{
    [Test]
    public async Task Deterministic_clock_steps()
    {
        var clock = new DeterministicFrameClock();
        clock.Step(3);
        await Assert.That(clock.Time).IsGreaterThan(0);
    }
}
