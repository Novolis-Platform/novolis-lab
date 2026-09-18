using System.Numerics;
using XFighter.Game;

namespace XFighter.Tests;

public sealed class CombatSystemTests
{
    [Test]
    public async Task SegmentHitsSphere_when_bolt_crosses_center()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(10, 0, 0);
        var center = new Vector3(5, 0, 0);
        var hit = CombatSystem.SegmentHitsSphere(start, end, center, 2f);
        await Assert.That(hit).IsTrue();
    }

    [Test]
    public async Task SegmentHitsSphere_misses_when_far_from_segment()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(10, 0, 0);
        var center = new Vector3(5, 8, 0);
        var hit = CombatSystem.SegmentHitsSphere(start, end, center, 1f);
        await Assert.That(hit).IsFalse();
    }
}
