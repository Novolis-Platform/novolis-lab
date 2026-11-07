namespace ArtillerySimulator.Game;

/// <summary>Multi-octave procedural relief — Afghan highlands and Nordic ridge/valley character.</summary>
internal static class TerrainHeightSampler
{
    public static float Sample(float x, float z, int seed, TerrainStyle style)
    {
        return style switch
        {
            TerrainStyle.AfghanHighland => SampleAfghan(x, z, seed),
            TerrainStyle.NordicRidges => SampleNordic(x, z, seed),
            _ => SampleRuggedBlend(x, z, seed),
        };
    }

    private static float SampleRuggedBlend(float x, float z, int seed) =>
        SampleAfghan(x, z, seed) * 0.58f + SampleNordic(x, z, seed) * 0.42f;

    private static float SampleAfghan(float x, float z, int seed)
    {
        var nx = x * 0.00135f + seed * 0.11f;
        var nz = z * 0.00118f + seed * 0.17f;
        var macro = Fbm(nx, nz, seed, 5) * 340f;
        var meso = Fbm(nx * 3.4f, nz * 2.9f, seed + 17, 4) * 155f;
        var ridges = Ridged(nx * 5.8f, nz * 5.2f, seed + 31, 4) * 210f;
        var detail = Fbm(nx * 14f, nz * 12f, seed + 53, 3) * 42f;
        var plateau = 260f;
        return plateau + macro + meso + ridges + detail;
    }

    private static float SampleNordic(float x, float z, int seed)
    {
        var nx = x * 0.00095f + seed * 0.09f;
        var nz = z * 0.00088f + seed * 0.14f;
        var macro = Fbm(nx, nz, seed + 101, 5) * 240f;
        var ridges = Ridged(nx * 4.2f, nz * 3.8f, seed + 211, 5) * 195f;
        var valleyNoise = Fbm(nx * 2.1f, nz * 1.9f, seed + 307, 3);
        var valleys = -MathF.Pow(MathF.Max(0f, 0.55f - valleyNoise), 1.6f) * 150f;
        var detail = Fbm(nx * 16f, nz * 14f, seed + 401, 3) * 38f;
        var baseLevel = 95f;
        return baseLevel + macro + ridges + valleys + detail;
    }

    private static float Fbm(float x, float z, int seed, int octaves, float lacunarity = 2.05f, float gain = 0.52f)
    {
        var sum = 0f;
        var amp = 1f;
        var freq = 1f;
        var norm = 0f;
        for (var o = 0; o < octaves; o++)
        {
            sum += amp * Noise(x * freq, z * freq, seed + o * 7919);
            norm += amp;
            amp *= gain;
            freq *= lacunarity;
        }

        return sum / MathF.Max(norm, 1e-6f);
    }

    private static float Ridged(float x, float z, int seed, int octaves)
    {
        var sum = 0f;
        var amp = 1f;
        var freq = 1f;
        var norm = 0f;
        for (var o = 0; o < octaves; o++)
        {
            var n = Noise(x * freq, z * freq, seed + o * 1337);
            var ridge = 1f - MathF.Abs(n);
            ridge *= ridge;
            sum += amp * ridge;
            norm += amp;
            amp *= 0.48f;
            freq *= 2.15f;
        }

        return sum / MathF.Max(norm, 1e-6f);
    }

    private static float Noise(float x, float z, int seed)
    {
        var ix = (int)MathF.Floor(x);
        var iz = (int)MathF.Floor(z);
        var fx = x - ix;
        var fz = z - iz;
        var ux = Smooth(fx);
        var uz = Smooth(fz);
        var v00 = Lattice(ix, iz, seed);
        var v10 = Lattice(ix + 1, iz, seed);
        var v01 = Lattice(ix, iz + 1, seed);
        var v11 = Lattice(ix + 1, iz + 1, seed);
        return Lerp(Lerp(v00, v10, ux), Lerp(v01, v11, ux), uz);
    }

    private static float Lattice(int cx, int cz, int seed)
    {
        unchecked
        {
            var n = cx * 374761393 + cz * 668265263 + seed * 982451653;
            n = (n ^ (n >> 13)) * 1274126177;
            n ^= n >> 16;
            return (n & 0xFFFF) / 65535f * 2f - 1f;
        }
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
