using System.Numerics;

namespace ArtillerySimulator.Game;

/// <summary>
/// Odderøya fort battery — south tip / Galgebakken ridge (not the north neck at Odderøyveien).
/// WGS84 checked against the PDF land/sea mask island (~58.134°N 8.009°E).
/// </summary>
internal static class KristiansandFort
{
    /// <summary>Southern Odderøya fort ridge (user green mark).</summary>
    public const double Latitude = 58.1345;

    public const double Longitude = 8.0085;

    public const string Address = "Odderøya fort / Galgebakken, Kristiansand S";

    public static Vector2 ToWorldMeters()
    {
        var raw = KristiansandHeightSampler.ToWorldMeters(Latitude, Longitude);
        return SnapToFortHill(raw.X, raw.Y);
    }

    private static Vector2 SnapToFortHill(float x0, float z0)
    {
        const float radius = 120f;
        const float step = 12f;
        var bestX = x0;
        var bestZ = z0;
        var bestScore = float.NegativeInfinity;
        var found = false;

        for (var z = z0 - radius; z <= z0 + radius; z += step)
        {
            for (var x = x0 - radius; x <= x0 + radius; x += step)
            {
                if (x < 40f || z < 40f
                    || x > KristiansandHeightSampler.EastMeters - 40f
                    || z > KristiansandHeightSampler.NorthMeters - 40f)
                    continue;

                if (KristiansandLandSeaMask.IsWater(x, z))
                    continue;

                var h = KristiansandHeightSampler.SampleMetersStatic(x, z);
                // Odderøya is low coastal rock — reject inland mountains.
                if (h < 5f || h > 50f)
                    continue;

                // Prefer peninsula: water nearby west/east/south.
                var waterNear = 0;
                if (KristiansandLandSeaMask.IsWater(x - 140f, z)) waterNear++;
                if (KristiansandLandSeaMask.IsWater(x + 140f, z)) waterNear++;
                if (KristiansandLandSeaMask.IsWater(x, z - 160f)) waterNear++;
                if (waterNear < 2)
                    continue;

                var dx = x - x0;
                var dz = z - z0;
                var dist = MathF.Sqrt(dx * dx + dz * dz);
                var score = h + waterNear * 8f - dist * 0.4f;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestX = x;
                bestZ = z;
                found = true;
            }
        }

        return found ? new Vector2(bestX, bestZ) : new Vector2(x0, z0);
    }
}
