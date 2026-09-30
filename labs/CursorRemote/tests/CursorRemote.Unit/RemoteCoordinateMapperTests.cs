using CursorRemote.Services;

namespace CursorRemote.Unit;

public sealed class RemoteCoordinateMapperTests
{
    [Test]
    public async Task TryMap_MapsLetterboxedViewportToSource()
    {
        var ok = RemoteCoordinateMapper.TryMap(
            viewportWidth: 1000,
            viewportHeight: 1000,
            sourceWidth: 2000,
            sourceHeight: 1000,
            viewportX: 500,
            viewportY: 500,
            out var sourceX,
            out var sourceY);

        await Assert.That(ok).IsTrue();
        await Assert.That(sourceX).IsEqualTo(1000);
        await Assert.That(sourceY).IsEqualTo(500);
    }

    [Test]
    public async Task TryMap_RejectsTapInLetterbox()
    {
        var ok = RemoteCoordinateMapper.TryMap(
            viewportWidth: 1000,
            viewportHeight: 1000,
            sourceWidth: 2000,
            sourceHeight: 1000,
            viewportX: 10,
            viewportY: 100,
            out _,
            out _);

        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task TryMap_MapsZoomedCenterTap()
    {
        var ok = RemoteCoordinateMapper.TryMap(
            viewportWidth: 1000,
            viewportHeight: 1000,
            sourceWidth: 2000,
            sourceHeight: 1000,
            viewportX: 500,
            viewportY: 500,
            out var sourceX,
            out var sourceY,
            zoom: 2);

        await Assert.That(ok).IsTrue();
        await Assert.That(sourceX).IsEqualTo(1000);
        await Assert.That(sourceY).IsEqualTo(500);
    }

    [Test]
    public async Task TryMap_MapsPannedTap()
    {
        // At zoom 1, pan shifts the letterboxed image. Tap at viewport center
        // after panning left by 100 maps further right in source.
        var ok = RemoteCoordinateMapper.TryMap(
            viewportWidth: 1000,
            viewportHeight: 500,
            sourceWidth: 2000,
            sourceHeight: 1000,
            viewportX: 500,
            viewportY: 250,
            out var sourceX,
            out var sourceY,
            zoom: 1,
            panX: -100,
            panY: 0);

        await Assert.That(ok).IsTrue();
        await Assert.That(sourceX).IsEqualTo(1200);
        await Assert.That(sourceY).IsEqualTo(500);
    }

    [Test]
    public async Task TryMap_RejectsOutsidePannedContent()
    {
        // Full-bleed fit (no letterbox). Large pan shifts the image so a left-edge
        // tap falls outside the source.
        var ok = RemoteCoordinateMapper.TryMap(
            viewportWidth: 1000,
            viewportHeight: 500,
            sourceWidth: 2000,
            sourceHeight: 1000,
            viewportX: 50,
            viewportY: 250,
            out _,
            out _,
            zoom: 1,
            panX: 600,
            panY: 0);

        await Assert.That(ok).IsFalse();
    }
}
