namespace PulseStrip.Core;

using System.Numerics;
using Novolis.Simulation.Racing.Tracks;

/// <summary>
/// Validates a closed Catmull–Rom centerline before bake:
/// finite points (3D allowed), spacing, arc length, and non-degenerate tangents.
/// </summary>
public static class CenterSplineMath
{
    public readonly record struct ValidationResult(
        bool Ok,
        string Message,
        double ArcLength,
        int ControlPointCount,
        double MinSegmentLength,
        double MaxSegmentLength,
        double MeanCurvatureProxy);

    public static ValidationResult Validate(SplineLoop loop, int sampleCount = 2000)
    {
        var pts = loop.ControlPoints;
        if (pts.Count < 4)
            return Fail("Need at least 4 control points for a closed Catmull–Rom loop.", pts.Count);

        for (var i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            if (!IsFinite(p))
                return Fail($"Control point {i} is non-finite.", pts.Count);
            if (MathF.Abs(p.Y) > 5000f)
                return Fail($"Control point {i} elevation absurd (Y={p.Y}).", pts.Count);
        }

        double minSeg = double.MaxValue, maxSeg = 0;
        for (var i = 0; i < pts.Count; i++)
        {
            var d = Vector3.Distance(pts[i], pts[(i + 1) % pts.Count]);
            minSeg = Math.Min(minSeg, d);
            maxSeg = Math.Max(maxSeg, d);
        }

        if (minSeg < 1e-2)
            return Fail($"Control points too close (min segment {minSeg:F4}).", pts.Count, minSeg, maxSeg);

        var sampler = new CatmullRomSplineSampler();
        var samples = sampler.SampleEvenly(loop, sampleCount);
        double arc = 0;
        double curvSum = 0;
        var curvN = 0;
        for (var i = 0; i < samples.Count; i++)
        {
            var a = samples[i];
            var b = samples[(i + 1) % samples.Count];
            if (!IsFinite(a.Position) || !IsFinite(a.Tangent) || a.Tangent.LengthSquared() < 1e-10f)
                return Fail($"Degenerate sample at {i}.", pts.Count, minSeg, maxSeg);

            arc += Vector3.Distance(a.Position, b.Position);
            var t0 = Vector3.Normalize(a.Tangent);
            var t1 = Vector3.Normalize(b.Tangent);
            var turn = Math.Clamp(1.0 - Vector3.Dot(t0, t1), 0, 2);
            curvSum += turn;
            curvN++;
        }

        if (arc < 10)
            return Fail($"Arc length too short ({arc:F1}).", pts.Count, minSeg, maxSeg, curvSum / Math.Max(1, curvN));

        // Closed loop: first/last sample should be near each other on a closed spline.
        var closeGap = Vector3.Distance(samples[0].Position, samples[^1].Position);
        // Even sampling wraps; gap between last and first along the arc is already in arc.
        // Check that control hull is centered in a sane AABB.
        var min = pts[0];
        var max = pts[0];
        foreach (var p in pts)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        var extent = max - min;
        if (extent.X < 1 || extent.Z < 1)
            return Fail("Control hull has collapsed extent.", pts.Count, minSeg, maxSeg, curvSum / curvN);

        return new ValidationResult(
            Ok: true,
            Message: $"OK arc={arc:F1} pts={pts.Count} gap0={closeGap:F2}",
            ArcLength: arc,
            ControlPointCount: pts.Count,
            MinSegmentLength: minSeg,
            MaxSegmentLength: maxSeg,
            MeanCurvatureProxy: curvSum / curvN);
    }

    public static double MeasureArcLength(SplineLoop loop, int sampleCount = 2000)
    {
        var v = Validate(loop, sampleCount);
        return v.ArcLength;
    }

    private static ValidationResult Fail(
        string message,
        int count,
        double minSeg = 0,
        double maxSeg = 0,
        double curv = 0) =>
        new(false, message, 0, count, minSeg, maxSeg, curv);

    private static bool IsFinite(Vector3 v) =>
        float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
