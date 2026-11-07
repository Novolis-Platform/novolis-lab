using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// Old Odderøya Fort — Lasaretthøyden / festningsmurene on the northern island ridge,
/// overlooking Østre havn toward Kvadraturen (not Galgebakken south tip).
/// </summary>
internal static class KristiansandFort
{
    /// <summary>Lasaretthøyden fort ridge (WGS84), probed against NDH + water mask.</summary>
    public const double Latitude = 58.13650;

    public const double Longitude = 8.00450;

    public const string Address = "Odderøya fort (Lasaretthøyden), Kristiansand S";

    public static Vector2 ToWorldMeters()
    {
        var raw = KristiansandHeightSampler.ToWorldMeters(Latitude, Longitude);
        return SnapToFortGround(raw.X, raw.Y);
    }

    /// <summary>
    /// Stay on fort rock near the seed — ridge height OK; never walk out to Galgebakken tip.
    /// </summary>
    private static Vector2 SnapToFortGround(float x0, float z0)
    {
        const float radius = 160f;
        const float step = 8f;
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
                // Fort terrace / ridge — reject flats and Odderøytoppen peak.
                if (h < 6f || h > 70f)
                    continue;

                var dx = x - x0;
                var dz = z - z0;
                var dist = MathF.Sqrt(dx * dx + dz * dz);
                // Prefer near seed and usable battery height (~10–40 m).
                var heightFit = 1f - MathF.Abs(h - 22f) / 40f;
                var score = heightFit * 25f + h * 0.2f - dist * 1.1f;
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
