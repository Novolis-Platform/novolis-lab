using System.Numerics;

namespace ArtillerySimulator.Game;

/// <summary>
/// Odderøya south tip / Galgebakken seaward rock — not the inland 80 m ridge.
/// WGS84 chosen so mask shows water south + peninsula sides and NDH is low coastal rock.
/// </summary>
internal static class KristiansandFort
{
    /// <summary>Southern Odderøya tip (user green mark) — probed against KLSM + NDH.</summary>
    public const double Latitude = 58.13216;

    public const double Longitude = 8.01088;

    public const string Address = "Odderøya south tip / Galgebakken, Kristiansand S";

    public static Vector2 ToWorldMeters()
    {
        var raw = KristiansandHeightSampler.ToWorldMeters(Latitude, Longitude);
        return SnapToSouthTip(raw.X, raw.Y);
    }

    /// <summary>
    /// Keep the gun on low coastal rock with water to the south — never climb inland hills.
    /// </summary>
    private static Vector2 SnapToSouthTip(float x0, float z0)
    {
        const float radius = 280f;
        const float step = 16f;
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
                // Odderøya tip is low coastal rock — reject inland ridges (~80 m).
                if (h < 3f || h > 35f)
                    continue;

                var waterSouth = KristiansandLandSeaMask.IsWater(x, z - 140f);
                var waterWest = KristiansandLandSeaMask.IsWater(x - 140f, z);
                var waterEast = KristiansandLandSeaMask.IsWater(x + 140f, z);
                var waterNear = (waterSouth ? 1 : 0) + (waterWest ? 1 : 0) + (waterEast ? 1 : 0);
                if (!waterSouth || waterNear < 2)
                    continue;

                var dx = x - x0;
                var dz = z - z0;
                var dist = MathF.Sqrt(dx * dx + dz * dz);
                // Prefer further south (lower Z), then closeness to seed, then modest height.
                var score = (z0 - z) * 2.5f + waterNear * 10f - dist * 0.35f + h * 0.15f;
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
