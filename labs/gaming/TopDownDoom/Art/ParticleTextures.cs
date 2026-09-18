using Novolis.Math.Geometry;
using Novolis.Rendering.TwoD;

namespace TopDownDoom.Art;

internal sealed class ParticleTextures(
    TwoDTextureId softGlow,
    TwoDTextureId spark,
    TwoDTextureId smoke,
    TwoDTextureId shell)
{
    public TwoDTextureId SoftGlow { get; } = softGlow;
    public TwoDTextureId Spark { get; } = spark;
    public TwoDTextureId Smoke { get; } = smoke;
    public TwoDTextureId Shell { get; } = shell;

    public static ParticleTextures Create(TwoDTextureRegistry registry)
    {
        const int size = 16;
        var soft = new Rgba32[size * size];
        var sparkPx = new Rgba32[size * size];
        var smokePx = new Rgba32[size * size];
        var shellPx = new Rgba32[size * size];
        var c = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - c;
                var dy = y - c;
                var d = MathF.Sqrt(dx * dx + dy * dy) / c;
                if (d <= 1f)
                {
                    var a = (byte)(255 * (1f - d) * (1f - d));
                    soft[y * size + x] = new Rgba32(255, 255, 255, a);
                    smokePx[y * size + x] = new Rgba32(80, 80, 90, (byte)(a * 0.65f));
                }

                if (MathF.Abs(dx) < 1.2f || MathF.Abs(dy) < 1.2f)
                {
                    var sa = (byte)(220 * (1f - MathF.Min(1f, (MathF.Abs(dx) + MathF.Abs(dy)) / 4f)));
                    sparkPx[y * size + x] = new Rgba32(255, 240, 180, sa);
                }

                if (x is >= 5 and <= 10 && y is >= 4 and <= 11)
                {
                    shellPx[y * size + x] = new Rgba32(220, 180, 60);
                }
            }
        }

        return new ParticleTextures(
            registry.Register(soft, size, size, "particle-soft"),
            registry.Register(sparkPx, size, size, "particle-spark"),
            registry.Register(smokePx, size, size, "particle-smoke"),
            registry.Register(shellPx, size, size, "particle-shell"));
    }
}
