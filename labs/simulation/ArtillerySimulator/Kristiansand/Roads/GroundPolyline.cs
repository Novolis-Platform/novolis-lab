using System.Numerics;

namespace ArtillerySimulator;

/// <summary>A named river or road polyline in world meters. X is east, Y of the vector is north (world Z).</summary>
internal sealed class GroundPolyline
{
    public GroundPolyline(string name, string kind, Vector2[] points)
    {
        Name = name;
        Kind = kind;
        Points = points;
        Length = 0f;
        for (var i = 1; i < points.Length; i++)
            Length += Vector2.Distance(points[i - 1], points[i]);
    }

    public string Name { get; }

    public string Kind { get; }

    public Vector2[] Points { get; }

    public float Length { get; }

    public Vector2 At(float arc)
    {
        if (Points.Length == 0)
            return default;

        if (arc <= 0f || Points.Length == 1)
            return Points[0];

        var remaining = arc;
        for (var i = 1; i < Points.Length; i++)
        {
            var span = Vector2.Distance(Points[i - 1], Points[i]);
            if (remaining <= span || i == Points.Length - 1)
            {
                var t = span < 1e-3f ? 0f : Math.Clamp(remaining / span, 0f, 1f);
                return Vector2.Lerp(Points[i - 1], Points[i], t);
            }

            remaining -= span;
        }

        return Points[^1];
    }

    public float NearestArc(Vector2 point)
    {
        var best = float.MaxValue;
        var arc = 0f;
        var traveled = 0f;
        for (var i = 1; i < Points.Length; i++)
        {
            var a = Points[i - 1];
            var b = Points[i];
            var span = Vector2.Distance(a, b);
            var t = span < 1e-3f ? 0f : Math.Clamp(Vector2.Dot(point - a, b - a) / (span * span), 0f, 1f);
            var closest = Vector2.Lerp(a, b, t);
            var distance = Vector2.Distance(point, closest);
            if (distance < best)
            {
                best = distance;
                arc = traveled + span * t;
            }

            traveled += span;
        }

        return arc;
    }
}
