using Novolis.Math.Geometry;
using Novolis.Rendering.TwoD;

namespace TapDuelFootball.Art;

/// <summary>Brown American football with stripes and laces for the duel sprite.</summary>
internal static class ProceduralFootball
{
    public static TwoDTextureId Register(TwoDTextureRegistry registry, int size = 64)
    {
        var pixels = new Rgba32[size * size];
        var cx = (size - 1) * 0.5f;
        var cy = (size - 1) * 0.5f;
        var rx = size * 0.38f;
        var rz = size * 0.48f;
        var leather = new Rgba32(120, 72, 36);
        var outline = new Rgba32(28, 18, 10);
        var stripe = new Rgba32(245, 245, 245);
        var lace = new Rgba32(250, 250, 250);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var nx = (x - cx) / rx;
                var nz = (y - cy) / rz;
                var d = nx * nx + nz * nz;
                if (d > 1.05f)
                {
                    continue;
                }

                var color = leather;
                if (d > 0.88f)
                {
                    color = outline;
                }
                else if (MathF.Abs(nz) > 0.55f && MathF.Abs(nz) < 0.72f && MathF.Abs(nx) < 0.55f)
                {
                    color = stripe;
                }
                else if (MathF.Abs(nx) < 0.08f && MathF.Abs(nz) < 0.35f)
                {
                    color = lace;
                }
                else if (MathF.Abs(nz) < 0.28f && MathF.Abs(nx) < 0.22f && (int)(nz * 18f) % 2 == 0)
                {
                    color = lace;
                }

                pixels[y * size + x] = color;
            }
        }

        return registry.Register(pixels, size, size, "football");
    }
}
