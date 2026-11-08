using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Physics.Abstractions;

namespace ArtillerySimulator;

/// <summary>Coastal roads for show only — never used as OPFOR spawn graph.</summary>
internal static class OslofjordenDecorativeRoads
{
    private static readonly Color Road = Color.FromArgb(210, 52, 52, 52);

    public static WorldLineVertex[] Build(IHeightSampler heights)
    {
        var batch = new SilkWireBatch();
        foreach (var poly in BuildCoastalLoops())
            Drape(batch, heights, poly);
        return batch.AsSpan().ToArray();
    }

    private static IEnumerable<Vector2[]> BuildCoastalLoops()
    {
        var east = OslofjordenGeodesy.EastMeters;
        var north = OslofjordenGeodesy.NorthMeters;
        // West bank road (Hurum side) and east bank road (Drøbak / Frogn).
        yield return TraceBank(west: true, east, north, samples: 48);
        yield return TraceBank(west: false, east, north, samples: 48);
        // Short harbour loops near Drøbak latitude.
        var drøbak = OslofjordenGeodesy.ToWorldMeters(59.663, 10.632);
        yield return LocalLoop(drøbak, radius: 480f, samples: 16);
    }

    private static Vector2[] TraceBank(bool west, float east, float north, int samples)
    {
        var pts = new List<Vector2>(samples);
        for (var i = 0; i < samples; i++)
        {
            var t = i / (float)(samples - 1);
            var z = 200f + t * (north - 400f);
            var center = OslofjordenGeodesy.ToWorldMeters(
                OslofjordenGeodesy.SouthLatitude + t * (OslofjordenGeodesy.NorthLatitude - OslofjordenGeodesy.SouthLatitude),
                west ? OslofjordenGeodesy.WestLongitude + 0.04 : OslofjordenGeodesy.EastLongitude - 0.04);
            // Pull toward shore from inland seed.
            var fjordX = east * (0.46f + 0.04f * MathF.Sin(t * MathF.PI * 1.6f));
            var pinchAt = OslofjordenGeodesy.OscarsborgNorthFraction;
            var half = 3200f - 2100f * MathF.Exp(-MathF.Pow((t - pinchAt) / 0.08f, 2f));
            var x = west ? fjordX - half - 220f : fjordX + half + 220f;
            x = Math.Clamp(x, 80f, east - 80f);
            z = Math.Clamp(z, 80f, north - 80f);
            _ = center;
            pts.Add(new Vector2(x, z));
        }

        return pts.ToArray();
    }

    private static Vector2[] LocalLoop(Vector2 center, float radius, int samples)
    {
        var pts = new Vector2[samples + 1];
        for (var i = 0; i <= samples; i++)
        {
            var a = i / (float)samples * MathF.PI * 2f;
            pts[i] = new Vector2(center.X + MathF.Cos(a) * radius, center.Y + MathF.Sin(a) * radius * 0.55f);
        }

        return pts;
    }

    private static void Drape(SilkWireBatch wires, IHeightSampler heights, Vector2[] points)
    {
        for (var i = 1; i < points.Length; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            if (OslofjordenSurface.IsWater(a.X, a.Y) || OslofjordenSurface.IsWater(b.X, b.Y))
                continue;
            var pa = new Vector3(a.X, heights.SampleHeight(a.X, a.Y) + 6f, a.Y);
            var pb = new Vector3(b.X, heights.SampleHeight(b.X, b.Y) + 6f, b.Y);
            wires.Line(pa, pb, Road);
        }
    }
}
