using System.Drawing;
using System.Numerics;
using Novolis.Silk;

namespace ArtillerySimulator;

internal static class BallisticArcWire
{
    public static void Draw(SilkWireBatch wires, IReadOnlyList<Vector3> points, Color color, int maxSegments = 180)
    {
        if (points.Count < 2)
            return;

        // Always connect consecutive samples — preview already downsamples to ≤512.
        // Never stride-skip here; that turned a lofted parabola into two chords on overview cams.
        var budget = Math.Max(2, maxSegments + 1);
        var stride = points.Count <= budget
            ? 1
            : Math.Max(1, (points.Count - 1 + maxSegments - 1) / maxSegments);
        if (stride == 1)
        {
            for (var i = 1; i < points.Count; i++)
                wires.Line(points[i - 1], points[i], color);
            return;
        }

        for (var i = stride; i < points.Count; i += stride)
            wires.Line(points[i - stride], points[i], color);

        var last = points.Count - 1;
        var drawn = last - (last % stride);
        if (drawn != last && drawn >= 0)
            wires.Line(points[drawn], points[last], color);
    }
}
