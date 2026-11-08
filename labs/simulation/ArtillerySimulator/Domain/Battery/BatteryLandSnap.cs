using System.Numerics;

namespace ArtillerySimulator;

/// <summary>Keeps battery pads on land when offsets land in the fairway.</summary>
internal static class BatteryLandSnap
{
    public static Vector2 Snap(TerrainWorld terrain, float x, float z, float radius = 280f, float step = 20f)
    {
        if (PlayableMapBounds.InPlayableMap(terrain, x, z) && !terrain.Profile.IsWater(x, z))
            return new Vector2(x, z);

        var best = new Vector2(x, z);
        var bestScore = float.NegativeInfinity;
        for (var dz = -radius; dz <= radius; dz += step)
        {
            for (var dx = -radius; dx <= radius; dx += step)
            {
                var px = x + dx;
                var pz = z + dz;
                if (!PlayableMapBounds.InPlayableMap(terrain, px, pz))
                    continue;
                if (terrain.Profile.IsWater(px, pz))
                    continue;
                var h = terrain.Field.SampleHeight(px, pz);
                if (h < 2f)
                    continue;
                var dist = MathF.Sqrt(dx * dx + dz * dz);
                var score = h * 0.2f - dist;
                if (score <= bestScore)
                    continue;
                bestScore = score;
                best = new Vector2(px, pz);
            }
        }

        return best;
    }
}
