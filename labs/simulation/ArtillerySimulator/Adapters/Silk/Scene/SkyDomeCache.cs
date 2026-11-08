using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>Regenerates the world-space sky dome when the camera drifts far from the last center.</summary>
internal sealed class SkyDomeCache : IDisposable
{
    private static readonly Rgba32 SkyZenith = new(28, 58, 98, 255);
    private static readonly Rgba32 SkyHorizon = new(210, 140, 90, 220);

    private readonly WorldSolidBatch _solids = new();
    private WorldSolidGpuMesh? _skyMesh;
    private Vector3 _skyCenter;

    public void Ensure(SilkFrame frame, Vector3 camera, Vector3 gunBaseline)
    {
        var center = new Vector3(camera.X, MathF.Min(camera.Y, gunBaseline.Y + 40f), camera.Z);
        if (_skyMesh is not null && Vector3.DistanceSquared(center, _skyCenter) < 500f * 500f)
            return;

        _skyMesh?.Dispose();
        _solids.Clear();
        _solids.SkyDome(center, radius: 28_000f, SkyZenith, SkyHorizon, slices: 24, stacks: 8);
        _skyMesh = frame.CreateWorldSolidMesh(_solids.Triangles);
        _skyCenter = center;
    }

    public void DrawIfReady(SilkFrame frame, Matrix4x4 mvp)
    {
        if (_skyMesh is not null)
            frame.DrawWorldSolidMesh(_skyMesh, mvp);
    }

    public void Dispose() => _skyMesh?.Dispose();
}
