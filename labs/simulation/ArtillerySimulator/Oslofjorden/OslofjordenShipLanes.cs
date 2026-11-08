using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// Invisible naval fairways for the 1940 invasion fleet.
/// All tracks run south → north (outer fjord → Drøbak pinch → Oslo approaches).
/// Gameplay only — never drawn as ribbons (roads are decorative).
/// </summary>
internal static class OslofjordenShipLanes
{
    public static IReadOnlyList<GroundPolyline> BuildFairways()
    {
        var lanes = new List<GroundPolyline>(6)
        {
            Fairway("Main channel", Track(offsetX: 0f, samples: 40)),
            Fairway("West channel", Track(offsetX: -520f, samples: 36)),
            Fairway("East channel", Track(offsetX: 480f, samples: 36)),
            Fairway("Destroyer screen", Track(offsetX: 780f, samples: 32)),
            Fairway("Night approach", Track(offsetX: -760f, samples: 32)),
        };
        return lanes;
    }

    private static GroundPolyline Fairway(string name, Vector2[] points) =>
        new(name, "fairway", points);

    private static Vector2[] Track(float offsetX, int samples)
    {
        var east = OslofjordenGeodesy.EastMeters;
        var north = OslofjordenGeodesy.NorthMeters;
        var pinchAt = OslofjordenGeodesy.OscarsborgNorthFraction;
        var pts = new Vector2[samples];
        // Deep southern entry; end just shy of the north gate.
        var z0 = 320f;
        var z1 = north - 380f;
        for (var i = 0; i < samples; i++)
        {
            var t = i / (float)(samples - 1);
            var z = z0 + t * (z1 - z0);
            var u = Math.Clamp(z / north, 0f, 1f);
            var center = east * (0.46f + 0.04f * MathF.Sin(u * MathF.PI * 1.6f));
            var half = 3200f - 2100f * MathF.Exp(-MathF.Pow((u - pinchAt) / 0.08f, 2f));
            var x = Math.Clamp(center + offsetX, center - half * 0.72f, center + half * 0.72f);
            pts[i] = new Vector2(x, z);
        }

        return pts;
    }
}
