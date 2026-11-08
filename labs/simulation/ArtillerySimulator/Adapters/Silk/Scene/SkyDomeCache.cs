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
    private float _skyRadius;

    public void Ensure(SilkFrame frame, Vector3 camera, Vector3 gunBaseline, float mapExtentMeters)
    {
        // Oslofjorden spans ~40 km; a short dome reads as nearby orange "mountains" from overview cam.
        var radius = MathF.Max(120_000f, mapExtentMeters * 4.5f);
        var center = new Vector3(camera.X, gunBaseline.Y + 40f, camera.Z);
        if (_skyMesh is not null
            && MathF.Abs(radius - _skyRadius) < 500f
            && Vector3.DistanceSquared(center, _skyCenter) < 2_000f * 2_000f)
            return;

        _skyMesh?.Dispose();
        _solids.Clear();
        _solids.SkyDome(center, radius, SkyZenith, SkyHorizon, slices: 48, stacks: 16);
        _skyMesh = frame.CreateWorldSolidMesh(_solids.Triangles);
        _skyCenter = center;
        _skyRadius = radius;
    }

    public void DrawIfReady(SilkFrame frame, Matrix4x4 mvp)
    {
        if (_skyMesh is not null)
            frame.DrawWorldSolidMesh(_skyMesh, mvp);
    }

    public void Dispose() => _skyMesh?.Dispose();
}
