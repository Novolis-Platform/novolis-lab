using System.Numerics;
using ArtillerySimulator.Game;
using Novolis.Math.Geometry;
using Novolis.Silk;

namespace ArtilleryMapSilkLab;

/// <summary>
/// Silk host for the Kristiansand wire mesh: black universe, green land / blue water, dense DEM wires.
/// Drag orbit, wheel zoom. R resets view.
/// </summary>
internal static class Program
{
    private const int DrawCells = 768;

    private static readonly Rgba32 LandEast = new(58, 88, 52);
    private static readonly Rgba32 LandNorth = new(88, 132, 68);
    private static readonly Rgba32 WaterEast = new(48, 110, 210);
    private static readonly Rgba32 WaterNorth = new(81, 130, 255);

    public static void Main()
    {
        var sampler = new KristiansandHeightSampler();
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        var lines = BuildWireLines(sampler, east, north, DrawCells);

        // High orbit so the draped XZ grid reads as a map, not a cliff of stems.
        var yaw = MathF.PI;
        var pitch = 1.22f;
        var distance = MathF.Max(east, north) * 1.15f;
        var target = new Vector3(east * 0.5f, 80f, north * 0.5f);

        SilkGame.Run("Kristiansand wireframe — Silk", 1440, 900, initialize: null, update: frame =>
        {
            if (frame.IsKeyPressed(Key.R))
            {
                yaw = MathF.PI;
                pitch = 1.22f;
                distance = MathF.Max(east, north) * 1.15f;
            }

            if (frame.IsMouseButtonDown(MouseButton.Left) || frame.IsMouseButtonDown(MouseButton.Right))
            {
                yaw += frame.MouseDeltaX * 0.0045f;
                pitch = Math.Clamp(pitch + frame.MouseDeltaY * 0.0035f, 0.35f, 1.48f);
            }

            distance = Math.Clamp(distance * (1f - frame.ScrollDelta * 0.08f), 800f, MathF.Max(east, north) * 2.4f);

            var eye = target + new Vector3(
                MathF.Sin(yaw) * MathF.Cos(pitch) * distance,
                MathF.Sin(pitch) * distance,
                MathF.Cos(yaw) * MathF.Cos(pitch) * distance);

            var aspect = frame.Width / (float)Math.Max(1, frame.Height);
            var view = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY);
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(42f * (MathF.PI / 180f), aspect, 40f, distance * 8f);
            var mvp = view * proj;

            frame.Clear(Rgba32.Black);
            frame.DrawWorldLines(lines, mvp);
            frame.SetTitle($"Kristiansand wireframe — Silk  {DrawCells}×{DrawCells}  relief×{KristiansandHeightSampler.VisualReliefScale:0}");
        });
    }

    private static WorldLineVertex[] BuildWireLines(KristiansandHeightSampler sampler, float east, float north, int cells)
    {
        var stride = cells + 1;
        var grid = new Vector3[stride * stride];
        for (var z = 0; z <= cells; z++)
        {
            for (var x = 0; x <= cells; x++)
            {
                var fx = x / (float)cells * east;
                var fz = z / (float)cells * north;
                grid[z * stride + x] = new Vector3(fx, sampler.SampleHeight(fx, fz), fz);
            }
        }

        var list = new List<WorldLineVertex>(cells * stride * 4);
        for (var z = 0; z <= cells; z++)
        {
            for (var x = 0; x < cells; x++)
            {
                var a = grid[z * stride + x];
                var b = grid[z * stride + x + 1];
                var color = SegmentWater(a, b) ? WaterEast : LandEast;
                list.Add(ToVert(a, color));
                list.Add(ToVert(b, color));
            }
        }

        for (var x = 0; x <= cells; x++)
        {
            for (var z = 0; z < cells; z++)
            {
                var a = grid[z * stride + x];
                var b = grid[(z + 1) * stride + x];
                var color = SegmentWater(a, b) ? WaterNorth : LandNorth;
                list.Add(ToVert(a, color));
                list.Add(ToVert(b, color));
            }
        }

        return list.ToArray();
    }

    private static bool SegmentWater(Vector3 a, Vector3 b)
    {
        var mid = (a + b) * 0.5f;
        var aW = KristiansandLandSeaMask.IsWater(a.X, a.Z);
        var bW = KristiansandLandSeaMask.IsWater(b.X, b.Z);
        if (aW != bW)
            return false;
        return aW && KristiansandLandSeaMask.IsWater(mid.X, mid.Z);
    }

    private static WorldLineVertex ToVert(Vector3 p, Rgba32 c) => new(p.X, p.Y, p.Z, c);
}
