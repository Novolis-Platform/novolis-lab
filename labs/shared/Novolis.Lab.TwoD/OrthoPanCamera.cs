using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Rendering.TwoD;

namespace Novolis.Lab.TwoD;

/// <summary>Top-down orthographic pan + zoom for RTS-style dogfood.</summary>
public sealed class OrthoPanCamera
{
    public Vector3 Center { get; set; }
    public float WorldUnitsPerPixel { get; set; } = 1f / 24f;
    public float MinWorldUnitsPerPixel { get; set; } = 1f / 48f;
    public float MaxWorldUnitsPerPixel { get; set; } = 1f / 12f;

    public void ApplyTo(TwoDViewport camera, int viewportWidth, int viewportHeight)
    {
        camera.Position = Center;
        camera.WorldUnitsPerPixel = WorldUnitsPerPixel;
        camera.ViewportWidth = viewportWidth;
        camera.ViewportHeight = viewportHeight;
    }

    public void Pan(Vector3 delta)
    {
        Center = new Vector3(Center.X + delta.X, 0f, Center.Z + delta.Z);
    }

    public void Zoom(float wheelSteps)
    {
        if (MathF.Abs(wheelSteps) < 1e-6f)
        {
            return;
        }

        var factor = MathF.Pow(1.08f, -wheelSteps);
        WorldUnitsPerPixel = System.Math.Clamp(WorldUnitsPerPixel * factor, MinWorldUnitsPerPixel, MaxWorldUnitsPerPixel);
    }

    public Vector3 ViewCenterWorld() => Center;

    public Vector3 ScreenToWorld(TwoDViewport camera, float screenX, float screenY) =>
        camera.ScreenToWorld(screenX, screenY);
}
