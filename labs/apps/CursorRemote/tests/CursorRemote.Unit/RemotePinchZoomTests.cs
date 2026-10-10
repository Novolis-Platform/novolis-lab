using CursorRemote.Services;

namespace CursorRemote.Unit;

public sealed class RemotePinchZoomTests
{
    [Test]
    public async Task FromDistances_StaysStableWhenFingerDistanceIsUnchanged()
    {
        const double baseZoom = 1;
        const double start = 100;
        var zoom = baseZoom;
        for (var i = 0; i < 20; i++)
        {
            zoom = RemotePinchZoom.FromDistances(
                baseZoom,
                start,
                currentDistance: 150,
                minimumZoom: 1,
                maximumZoom: 8);
        }

        await Assert.That(zoom).IsEqualTo(
            RemotePinchZoom.FromDistances(1, 100, 150, 1, 8));
        await Assert.That(zoom).IsLessThan(2.0);
        await Assert.That(zoom).IsGreaterThan(1.2);
    }

    [Test]
    public async Task FromDistances_DoesNotExplodeLikeRebasedCompounding()
    {
        // Old bug: each frame used the previous zoom as the new base while
        // still dividing by the original start distance → exponential growth.
        var buggy = 1.0;
        for (var i = 0; i < 8; i++)
            buggy *= 150.0 / 100.0;

        var fixedZoom = 1.0;
        for (var i = 0; i < 8; i++)
        {
            fixedZoom = RemotePinchZoom.FromDistances(
                baseZoom: 1,
                startDistance: 100,
                currentDistance: 150,
                minimumZoom: 1,
                maximumZoom: 8);
        }

        await Assert.That(buggy).IsGreaterThan(20);
        await Assert.That(fixedZoom).IsLessThan(2);
    }

    [Test]
    public async Task FromDistances_PinchInReducesZoom()
    {
        var zoom = RemotePinchZoom.FromDistances(
            baseZoom: 2,
            startDistance: 200,
            currentDistance: 100,
            minimumZoom: 1,
            maximumZoom: 8);

        await Assert.That(zoom).IsLessThan(2);
        await Assert.That(zoom).IsGreaterThanOrEqualTo(1);
    }
}
