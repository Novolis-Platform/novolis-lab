using System.Numerics;
using ArtillerySimulator.Game;
using Novolis.Math.Geometry;
using Novolis.Silk;

namespace ArtilleryMapSilkLab;

/// <summary>
/// Thin Kristiansand wire map viewer. Full mission/gun loop lives in ArtillerySimulator (Silk-only).
/// </summary>
internal static class Program
{
    private const int DrawCells = 1024;

    private static readonly Rgba32 LandEast = new(58, 88, 52);
    private static readonly Rgba32 LandNorth = new(88, 132, 68);
    private static readonly Rgba32 SeaEast = new(48, 110, 210);
    private static readonly Rgba32 SeaNorth = new(81, 130, 255);
    private static readonly Rgba32 LakeEast = new(40, 160, 190);
    private static readonly Rgba32 LakeNorth = new(70, 200, 210);
    private static readonly Rgba32 GunCopper = new(210, 132, 72);
    private static readonly Rgba32 GunTube = new(186, 176, 154);
    private static readonly Rgba32 GunCarriage = new(78, 86, 64);

    public static void Main()
    {
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        var lines = BuildWireLines(east, north, DrawCells);

        var fort = KristiansandFort.ToWorldMeters();
        var fortY = KristiansandSurface.SampleDrawHeight(fort.X, fort.Y);
        var gunPivot = new Vector3(fort.X, fortY + 1.6f, fort.Y);
        var downtown = KristiansandHeightSampler.ToWorldMeters(58.1464, 7.9956);
        var aim = Vector3.Normalize(new Vector3(downtown.X - fort.X, 0f, downtown.Y - fort.Y));
        var gunLines = BuildGunWires(gunPivot, aim);

        var target = new Vector3(fort.X, fortY + 12f, fort.Y);
        var yaw = (float)(Math.PI + 20.0 * (Math.PI / 180.0));
        var pitch = 0.48f;
        var distance = 420f / MathF.Sin(Math.Max(pitch, 0.2f));

        SilkGame.Run("Artillery map — Odderøya fort (Silk)", 1440, 900, initialize: null, update: frame =>
        {
            if (frame.IsKeyPressed(Key.R) || frame.IsKeyPressed(Key.G))
            {
                target = new Vector3(fort.X, fortY + 12f, fort.Y);
                yaw = (float)(Math.PI + 20.0 * (Math.PI / 180.0));
                pitch = 0.48f;
                distance = Math.Clamp(420f / MathF.Sin(Math.Max(pitch, 0.2f)), 180f, 6_000f);
            }

            if (frame.IsKeyPressed(Key.O) || frame.IsKeyPressed(Key.V))
            {
                target = new Vector3(east * 0.5f, 80f, north * 0.5f);
                yaw = MathF.PI;
                pitch = 1.22f;
                distance = MathF.Max(east, north) * 1.15f;
            }

            if (frame.IsMouseButtonDown(MouseButton.Left) || frame.IsMouseButtonDown(MouseButton.Right))
            {
                yaw -= frame.MouseDeltaX * 0.0045f;
                pitch = Math.Clamp(pitch + frame.MouseDeltaY * 0.0035f, 0.12f, 1.45f);
            }

            distance = Math.Clamp(distance * (1f - frame.ScrollDelta * 0.08f), 80f, MathF.Max(east, north) * 2.4f);

            var eye = target + new Vector3(
                MathF.Sin(yaw) * MathF.Cos(pitch) * distance,
                MathF.Sin(pitch) * distance,
                MathF.Cos(yaw) * MathF.Cos(pitch) * distance);

            var aspect = frame.Width / (float)Math.Max(1, frame.Height);
            var view = KristiansandGeoref.CreateMapView(eye, target, Vector3.UnitY);
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(42f * (MathF.PI / 180f), aspect, 2f, MathF.Max(distance * 8f, 40_000f));
            var mvp = view * proj;

            frame.Clear(Rgba32.Black);
            frame.DrawWorldLines(lines, mvp);
            frame.DrawWorldLines(gunLines, mvp);
            frame.SetTitle("Odderøya fort gun — prefer ArtillerySimulator for full mission (R/G fort, O/V map)");
        });
    }

    private static WorldLineVertex[] BuildGunWires(Vector3 pivot, Vector3 aimHorizontal)
    {
        var list = new List<WorldLineVertex>(64);
        var elev = 22f * (MathF.PI / 180f);
        var dir = Vector3.Normalize(new Vector3(
            aimHorizontal.X * MathF.Cos(elev),
            MathF.Sin(elev),
            aimHorizontal.Z * MathF.Cos(elev)));
        var muzzle = pivot + dir * 5.2f;
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, dir));
        if (right.LengthSquared() < 1e-6f)
            right = Vector3.UnitX;
        var axle = pivot - new Vector3(0f, 0.85f, 0f);
        var trail = pivot - aimHorizontal * 3.6f - new Vector3(0f, 0.55f, 0f);

        void Seg(Vector3 a, Vector3 b, Rgba32 c)
        {
            list.Add(new WorldLineVertex(a.X, a.Y, a.Z, c));
            list.Add(new WorldLineVertex(b.X, b.Y, b.Z, c));
        }

        Seg(pivot + new Vector3(0f, 0.95f, 0f), muzzle, GunTube);
        Seg(axle + right * 0.35f, trail + right * 0.7f, GunCarriage);
        Seg(axle - right * 0.35f, trail - right * 0.7f, GunCarriage);
        Seg(axle + right * 1.15f, axle - right * 1.15f, GunCarriage);
        Seg(muzzle, muzzle + dir * 0.4f, GunCopper);
        Seg(pivot, pivot + Vector3.UnitY * 18f, GunCopper);
        return list.ToArray();
    }

    private static WorldLineVertex[] BuildWireLines(float east, float north, int cells)
    {
        var stride = cells + 1;
        var grid = new Vector3[stride * stride];
        var kinds = new KristiansandSurfaceKind[stride * stride];
        for (var z = 0; z <= cells; z++)
        {
            for (var x = 0; x <= cells; x++)
            {
                var fx = x / (float)cells * east;
                var fz = z / (float)cells * north;
                var meters = KristiansandHeightSampler.SampleMetersStatic(fx, fz);
                var kind = KristiansandSurface.KindAt(fx, fz, meters);
                kinds[z * stride + x] = kind;
                var y = kind == KristiansandSurfaceKind.Sea
                    ? 0f
                    : KristiansandSurface.ToDisplayHeight(meters);
                grid[z * stride + x] = new Vector3(fx, y, fz);
            }
        }

        var list = new List<WorldLineVertex>(cells * stride * 4);
        for (var z = 0; z <= cells; z++)
        {
            for (var x = 0; x < cells; x++)
            {
                var i0 = z * stride + x;
                AddSeg(list, grid[i0], grid[i0 + 1], kinds[i0], kinds[i0 + 1], eastWest: true);
            }
        }

        for (var x = 0; x <= cells; x++)
        {
            for (var z = 0; z < cells; z++)
            {
                var i0 = z * stride + x;
                AddSeg(list, grid[i0], grid[i0 + stride], kinds[i0], kinds[i0 + stride], eastWest: false);
            }
        }

        return list.ToArray();
    }

    private static void AddSeg(
        List<WorldLineVertex> list,
        Vector3 a,
        Vector3 b,
        KristiansandSurfaceKind ka,
        KristiansandSurfaceKind kb,
        bool eastWest)
    {
        var mid = (a + b) * 0.5f;
        var km = KristiansandSurface.KindAt(mid.X, mid.Z);
        KristiansandSurfaceKind kind;
        if (ka != kb)
            kind = KristiansandSurfaceKind.Land;
        else if (ka == KristiansandSurfaceKind.Sea && km == KristiansandSurfaceKind.Sea)
            kind = KristiansandSurfaceKind.Sea;
        else if (ka == KristiansandSurfaceKind.Lake && km == KristiansandSurfaceKind.Lake)
            kind = KristiansandSurfaceKind.Lake;
        else
            kind = KristiansandSurfaceKind.Land;

        var color = kind switch
        {
            KristiansandSurfaceKind.Sea => eastWest ? SeaEast : SeaNorth,
            KristiansandSurfaceKind.Lake => eastWest ? LakeEast : LakeNorth,
            _ => eastWest ? LandEast : LandNorth,
        };
        list.Add(new WorldLineVertex(a.X, a.Y, a.Z, color));
        list.Add(new WorldLineVertex(b.X, b.Y, b.Z, color));
    }
}
