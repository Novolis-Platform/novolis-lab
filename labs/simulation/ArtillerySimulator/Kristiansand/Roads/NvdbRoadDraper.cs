using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Physics.Abstractions;

namespace ArtillerySimulator;

/// <summary>Drapes NVDB polylines onto the heightfield as wire segments.</summary>
internal static class NvdbRoadDraper
{
    private const float LiftMeters = 8f;

    public static void DrapePolyline(
        SilkWireBatch wires,
        IHeightSampler heights,
        float east,
        float north,
        Vector2[] points,
        Color color,
        float drapeMeters)
    {
        if (points.Length < 2)
            return;

        for (var i = 1; i < points.Length; i++)
        {
            var a2 = points[i - 1];
            var b2 = points[i];
            if (!NvdbMapClip.ClipSegmentToMap(ref a2, ref b2, east, north))
                continue;

            var dx = b2.X - a2.X;
            var dz = b2.Y - a2.Y;
            var len = MathF.Sqrt(dx * dx + dz * dz);
            if (len < 0.5f)
                continue;

            if (len <= drapeMeters)
            {
                wires.Line(Place(heights, a2.X, a2.Y), Place(heights, b2.X, b2.Y), color);
                continue;
            }

            var samples = Math.Max(2, (int)MathF.Ceiling(len / drapeMeters) + 1);
            Vector3? prev = null;
            var inv = 1f / (samples - 1);

            for (var s = 0; s < samples; s++)
            {
                var t = s * inv;
                var x = a2.X + dx * t;
                var z = a2.Y + dz * t;
                var p = Place(heights, x, z);
                if (prev is { } p0)
                    wires.Line(p0, p, color);
                prev = p;
            }
        }
    }

    private static Vector3 Place(IHeightSampler heights, float x, float z) =>
        new(x, heights.SampleHeight(x, z) + LiftMeters, z);
}
