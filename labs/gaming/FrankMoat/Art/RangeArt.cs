using FrankMoat.Levels;
using Novolis.Math.Geometry;
using Novolis.Rendering.Appearance;
using Novolis.Rendering.TwoD;

namespace FrankMoat.Art;

internal sealed class RangeArt
{
    public required TwoDTextureId White { get; init; }
    public required TwoDTextureId SoftGlow { get; init; }
    public required TwoDTextureId Spark { get; init; }
    public required TwoDTextureId Smoke { get; init; }
    public required TwoDTextureId Shell { get; init; }
    public required TwoDTextureId Blood { get; init; }
    public required TwoDTextureId Frank { get; init; }
    public required TwoDTextureId Shambler { get; init; }
    public required TwoDTextureId Runner { get; init; }
    public required TwoDTextureId Tank { get; init; }
    public required TwoDTextureId Barrel { get; init; }
    public required TwoDTextureId Crate { get; init; }
    public required TwoDTextureId Floor { get; init; }
    public required TwoDTextureId Steel { get; init; }
    public required TwoDTextureId Concrete { get; init; }
    public required TwoDTextureId Glass { get; init; }
    public required TwoDTextureId FilthyGlow { get; init; }
    public required TwoDTextureId FlashCone { get; init; }

    public TwoDTextureId Wall(WallMaterial material) => material switch
    {
        WallMaterial.Steel => Steel,
        WallMaterial.Glass => Glass,
        WallMaterial.FilthyGlow => FilthyGlow,
        _ => Concrete,
    };

    public static RangeArt Create(TwoDTextureRegistry registry) => new()
    {
        White = Solid(registry, 255, 255, 255, 255, "white"),
        SoftGlow = Disc(registry, 16, 255, 255, 255, 255, falloff: 2f, "glow"),
        Spark = Cross(registry),
        Smoke = Disc(registry, 16, 90, 90, 96, 170, falloff: 1.4f, "smoke"),
        Shell = Capsule(registry),
        Blood = Disc(registry, 16, 170, 16, 20, 230, falloff: 1.8f, "blood"),
        Frank = Actor(registry, 72, 88, 52, 36, 44, 28, "frank"),
        Shambler = Actor(registry, 86, 104, 70, 48, 56, 40, "shambler"),
        Runner = Actor(registry, 70, 92, 58, 40, 52, 34, "runner"),
        Tank = Actor(registry, 96, 88, 72, 60, 48, 40, "tank"),
        Barrel = Drum(registry),
        Crate = Box(registry),
        Floor = Register(registry, AppearanceBaker.BakeSurface(AppearanceRecipes.RangeFloor(), 128, 128, 8f, 4), 128, "floor"),
        Steel = Register(registry, AppearanceBaker.BakeSurface(AppearanceRecipes.SteelPanels(), 128, 128, 2f, 11), 128, "steel"),
        Concrete = Register(registry, AppearanceBaker.BakeSurface(AppearanceRecipes.ConcreteGrime(), 128, 128, 2f, 21), 128, "concrete"),
        Glass = Register(registry, AppearanceBaker.BakeSurface(AppearanceRecipes.GlassPane(), 64, 64, 1.5f, 3), 64, "glass"),
        FilthyGlow = Register(registry, AppearanceBaker.BakeSurface(AppearanceRecipes.FilthyGlowWall(), 128, 128, 2f, 69), 128, "filthy"),
        FlashCone = Register(registry, AppearanceBaker.BakeLightVolume(AppearanceRecipes.Flashlight(), AppearanceRecipes.IndoorFog(), 96, 96, 1), 96, "cone"),
    };

    private static TwoDTextureId Register(TwoDTextureRegistry registry, Rgba32[] pixels, int size, string name) =>
        registry.Register(pixels, size, size, name);

    private static TwoDTextureId Solid(TwoDTextureRegistry registry, byte r, byte g, byte b, byte a, string name)
    {
        return registry.Register([new Rgba32(r, g, b, a)], 1, 1, name);
    }

    private static TwoDTextureId Disc(
        TwoDTextureRegistry registry,
        int size,
        byte r,
        byte g,
        byte b,
        byte a,
        float falloff,
        string name)
    {
        var px = new Rgba32[size * size];
        var c = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var d = MathF.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                if (d > 1f)
                {
                    continue;
                }

                var alpha = (byte)(a * MathF.Pow(1f - d, falloff));
                px[y * size + x] = new Rgba32(r, g, b, alpha);
            }
        }

        return registry.Register(px, size, size, name);
    }

    private static TwoDTextureId Cross(TwoDTextureRegistry registry)
    {
        const int size = 16;
        var px = new Rgba32[size * size];
        var c = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (MathF.Abs(x - c) < 1.3f || MathF.Abs(y - c) < 1.3f)
                {
                    var sa = (byte)(230 * (1f - MathF.Min(1f, (MathF.Abs(x - c) + MathF.Abs(y - c)) / 5f)));
                    px[y * size + x] = new Rgba32(255, 236, 170, sa);
                }
            }
        }

        return registry.Register(px, size, size, "spark");
    }

    private static TwoDTextureId Capsule(TwoDTextureRegistry registry)
    {
        const int size = 16;
        var px = new Rgba32[size * size];
        for (var y = 4; y <= 11; y++)
        {
            for (var x = 6; x <= 10; x++)
            {
                px[y * size + x] = new Rgba32(220, 176, 64);
            }
        }

        return registry.Register(px, size, size, "shell");
    }

    private static TwoDTextureId Actor(
        TwoDTextureRegistry registry,
        byte bodyR,
        byte bodyG,
        byte bodyB,
        byte vestR,
        byte vestG,
        byte vestB,
        string name)
    {
        const int size = 32;
        var px = new Rgba32[size * size];
        var c = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - c;
                var dy = y - (c + 1f);
                var d = MathF.Sqrt(dx * dx + dy * dy);
                if (d < 11f)
                {
                    var vest = MathF.Abs(dx) < 5.5f && dy is > -2f and < 7f;
                    px[y * size + x] = vest
                        ? new Rgba32(vestR, vestG, vestB)
                        : new Rgba32(bodyR, bodyG, bodyB);
                }

                var hx = x - c;
                var hy = y - (c - 7f);
                if (hx * hx + hy * hy < 16f)
                {
                    px[y * size + x] = new Rgba32((byte)(bodyR + 16), (byte)(bodyG + 10), (byte)(bodyB + 8));
                }
            }
        }

        for (var x = 16; x < 28; x++)
        {
            px[15 * size + x] = new Rgba32(36, 38, 40);
            px[16 * size + x] = new Rgba32(48, 50, 52);
        }

        return registry.Register(px, size, size, name);
    }

    private static TwoDTextureId Drum(TwoDTextureRegistry registry)
    {
        const int size = 24;
        var px = new Rgba32[size * size];
        var c = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var d = MathF.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                if (d < 10f)
                {
                    px[y * size + x] = d > 8.2f ? new Rgba32(90, 18, 16) : new Rgba32(176, 32, 28);
                }
            }
        }

        return registry.Register(px, size, size, "barrel");
    }

    private static TwoDTextureId Box(TwoDTextureRegistry registry)
    {
        const int size = 20;
        var px = new Rgba32[size * size];
        for (var y = 3; y < 17; y++)
        {
            for (var x = 3; x < 17; x++)
            {
                var edge = x is 3 or 16 || y is 3 or 16;
                px[y * size + x] = edge ? new Rgba32(70, 48, 28) : new Rgba32(132, 92, 48);
            }
        }

        return registry.Register(px, size, size, "crate");
    }
}
