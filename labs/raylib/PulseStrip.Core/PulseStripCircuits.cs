namespace PulseStrip.Core;

using System.Numerics;
using Novolis.Simulation.Racing.Tracks;

/// <summary>
/// PulseStrip mega-circuits: ~100× CompactOval arc length, 2× half-width,
/// with elevation, side weave, and Möbius swirl baked into the centerline.
/// </summary>
public static class PulseStripCircuits
{
    /// <summary>CompactOval reference arc (~121) × 100.</summary>
    public const double TargetArcLength = 12_150;

    /// <summary>CompactOval half-width (4) × 2.</summary>
    public const double TrackHalfWidth = 8.0;

    public static IReadOnlyList<(string DisplayName, ITrackDefinition Definition)> All { get; } =
    [
        ("Pulse Grand Prix", MegaGrandPrix.Instance),
        ("Neon Labyrinth", NeonLabyrinth.Instance),
    ];

    public static ITrackDefinition ByIndex(int index)
    {
        if (index < 0 || index >= All.Count)
            return All[0].Definition;
        return All[index].Definition;
    }

    public static string DisplayName(int index)
    {
        if (index < 0 || index >= All.Count)
            return All[0].DisplayName;
        return All[index].DisplayName;
    }

    /// <summary>
    /// Harmonic mega-ellipse with up/down, side-to-side weave (3D Catmull–Rom).
    /// Möbius twist is applied at ribbon/frame time via <see cref="MobiusTrackFrames"/>.
    /// </summary>
    public static Vector3[] BuildMegaGrandPrixControls(int controlPoints = 96)
    {
        const float a = 2444f;
        const float b = 1222f;
        var cx = a + 80f;
        var cz = b + 80f;
        var pts = new Vector3[controlPoints];
        for (var i = 0; i < controlPoints; i++)
        {
            var t = MathF.Tau * i / controlPoints;
            var wobble = 1f
                         + 0.12f * MathF.Sin(3f * t)
                         + 0.08f * MathF.Sin(5f * t)
                         + 0.05f * MathF.Cos(7f * t)
                         + 0.04f * MathF.Sin(2f * t + 0.6f);
            var stretch = 1f + 0.18f * MathF.Max(0f, MathF.Cos(t));
            var x = cx + MathF.Cos(t) * a * wobble * stretch;
            var z = cz + MathF.Sin(t) * b * wobble;

            // Side-to-side weave in the planar radial normal.
            var nx = MathF.Cos(t);
            var nz = MathF.Sin(t);
            var weave = 160f * MathF.Sin(4f * t) + 70f * MathF.Cos(6f * t);
            x += -nz * weave;
            z += nx * weave;

            // Up / down hills and valleys.
            var y = 240f * MathF.Sin(2f * t)
                    + 110f * MathF.Sin(5f * t + 0.4f)
                    + 55f * MathF.Cos(3f * t);

            pts[i] = new Vector3(x, y, z);
        }

        return pts;
    }

    /// <summary>Multi-hairpin labyrinth with elevation + weave, scaled to TargetArcLength.</summary>
    public static Vector3[] BuildNeonLabyrinthControls()
    {
        var pts = new List<Vector3>(64);
        void Add(float x, float y, float z) => pts.Add(new Vector3(x, y, z));

        Add(200, 40, 1400);
        Add(800, 180, 400);
        Add(1600, 320, 200);
        Add(2800, 120, 350);
        Add(3800, -80, 900);
        Add(4200, 200, 1600);
        Add(4000, 360, 2400);
        Add(3400, 220, 2800);
        Add(2600, 40, 2600);
        Add(2200, -120, 2000);
        Add(2400, 80, 1600);
        Add(2000, 260, 1200);
        Add(1400, 300, 1100);
        Add(1000, 140, 1500);
        Add(900, -40, 2100);
        Add(1200, 160, 2500);
        Add(1800, 340, 2700);
        Add(2400, 280, 3000);
        Add(3000, 60, 3200);
        Add(3600, -100, 3000);
        Add(4100, 80, 2400);
        Add(4300, 240, 1700);
        Add(4000, 300, 900);
        Add(3200, 160, 300);
        Add(2200, -20, 150);
        Add(1200, 100, 300);
        Add(500, 220, 800);
        Add(300, 80, 1400);

        // Extra side weave on every other point.
        for (var i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            var t = MathF.Tau * i / pts.Count;
            var weave = 90f * MathF.Sin(3f * t);
            pts[i] = new Vector3(p.X + weave, p.Y + 40f * MathF.Cos(2f * t), p.Z);
        }

        var loop = new SplineLoop(pts);
        var arc = CenterSplineMath.MeasureArcLength(loop, 1500);
        if (arc < 1)
            return pts.ToArray();

        var scale = (float)(TargetArcLength / arc);
        var cx = pts.Average(p => p.X);
        var cy = pts.Average(p => p.Y);
        var cz = pts.Average(p => p.Z);
        for (var i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            pts[i] = new Vector3(
                cx + (p.X - cx) * scale,
                cy + (p.Y - cy) * scale,
                cz + (p.Z - cz) * scale);
        }

        return pts.ToArray();
    }

    private sealed class MegaGrandPrix : ITrackDefinition
    {
        public static readonly MegaGrandPrix Instance = new();
        public string Id => "pulse-grand-prix";
        public string Name => "Pulse Grand Prix";
        public TrackBuildSpec BuildSpec => TrackSpecs.Polyline(
            rasterWidth: 64,
            rasterHeight: 64,
            trackHalfWidth: TrackHalfWidth,
            wallThickness: 2.0,
            lapsToFinish: 3,
            controlPoints: BuildMegaGrandPrixControls(),
            gateCount: 24);
    }

    private sealed class NeonLabyrinth : ITrackDefinition
    {
        public static readonly NeonLabyrinth Instance = new();
        public string Id => "neon-labyrinth";
        public string Name => "Neon Labyrinth";
        public TrackBuildSpec BuildSpec => TrackSpecs.Polyline(
            rasterWidth: 64,
            rasterHeight: 64,
            trackHalfWidth: TrackHalfWidth,
            wallThickness: 2.0,
            lapsToFinish: 3,
            controlPoints: BuildNeonLabyrinthControls(),
            gateCount: 28);
    }
}
