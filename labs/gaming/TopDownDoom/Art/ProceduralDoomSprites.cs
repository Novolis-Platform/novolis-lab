using Novolis.Math.Geometry;
using Novolis.Rendering.TwoD;

namespace TopDownDoom.Art;

/// <summary>Built-in 8-way top-down characters (no rotation).</summary>
internal static class ProceduralDoomSprites
{
    private static readonly string[] DirectionSuffixes = ["down", "up", "right", "up_right", "down_right"];

    public static CharacterAnimationSet CreateMarine(TwoDTextureRegistry registry) =>
        CreateDirectional(registry, DoomArchetype.Marine, 0.68f);

    public static CharacterAnimationSet CreateZombie(TwoDTextureRegistry registry) =>
        CreateDirectional(registry, DoomArchetype.Zombie, 0.64f);

    public static CharacterAnimationSet CreateImp(TwoDTextureRegistry registry) =>
        CreateDirectional(registry, DoomArchetype.Imp, 0.58f);

    public static CharacterAnimationSet CreateBruiser(TwoDTextureRegistry registry) =>
        CreateDirectional(registry, DoomArchetype.Pinky, 0.82f);

    public static TwoDAnimationClip CreateExplosionClip(TwoDTextureRegistry registry)
    {
        const int fw = 32;
        const int fh = 32;
        const int frames = 8;
        var atlas = new Rgba32[fw * frames * fh];
        for (var f = 0; f < frames; f++)
        {
            var t = f / (float)(frames - 1);
            var radius = 4f + t * 12f;
            var alpha = (byte)(255 * (1f - t * 0.85f));
            var core = new Rgba32(255, 240, 120, alpha);
            var outer = new Rgba32(255, 90, 30, (byte)(alpha * 0.7f));
            for (var y = 0; y < fh; y++)
            {
                for (var x = 0; x < fw; x++)
                {
                    var ox = f * fw + x;
                    var dx = x - 16f;
                    var dy = y - 16f;
                    var d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d <= radius * 0.45f)
                    {
                        atlas[y * (fw * frames) + ox] = core;
                    }
                    else if (d <= radius)
                    {
                        atlas[y * (fw * frames) + ox] = outer;
                    }
                }
            }
        }

        var id = registry.Register(atlas, fw * frames, fh, "procedural-explosion");
        var sheet = new TwoDSpriteSheet(id, fw, fh, fw * frames, fh);
        return new TwoDAnimationClip(sheet, [0, 1, 2, 3, 4, 5, 6, 7], 18f);
    }

    public static TwoDTextureId CreatePickupIcon(TwoDTextureRegistry registry, PickupArtKind kind)
    {
        const int size = 32;
        var pixels = new Rgba32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                pixels[y * size + x] = kind switch
                {
                    PickupArtKind.Health => PixelMedkit(x, y),
                    PickupArtKind.Armor => PixelArmor(x, y),
                    PickupArtKind.Ammo => PixelAmmoBox(x, y),
                    PickupArtKind.BlueKey => PixelKey(x, y),
                    PickupArtKind.Exit => PixelExitPad(x, y),
                    PickupArtKind.Barrel => PixelBarrel(x, y),
                    _ => default,
                };
            }
        }

        return registry.Register(pixels, size, size, kind.ToString());
    }

    private static CharacterAnimationSet CreateDirectional(
        TwoDTextureRegistry registry,
        DoomArchetype archetype,
        float worldHalfHeight)
    {
        var facing = new DirectionalClips { WorldHalfHeight = worldHalfHeight };
        foreach (var suffix in DirectionSuffixes)
        {
            var view = ViewFromSuffix(suffix);
            var idle = BuildDirectionClip(registry, archetype, view, frames: 6, fps: 9f, walkCycle: true);
            var move = BuildDirectionClip(registry, archetype, view, frames: 6, fps: 12f, walkCycle: true);
            facing.AddIdle(suffix, idle);
            facing.AddMove(suffix, move);
        }

        var shoot = BuildDirectionClip(registry, archetype, ViewFromSuffix("down"), frames: 4, fps: 14f, walkCycle: false, shooting: true);
        facing.ShootOverlay = shoot;
        var fallback = facing.Select(0f, false, false).Clip;
        return new CharacterAnimationSet(fallback, worldHalfHeight, shoot, facing: facing);
    }

    private static TwoDAnimationClip BuildDirectionClip(
        TwoDTextureRegistry registry,
        DoomArchetype archetype,
        ViewAngle view,
        int frames,
        float fps,
        bool walkCycle,
        bool shooting = false)
    {
        const int fw = 48;
        const int fh = 48;
        var atlas = new Rgba32[fw * frames * fh];
        for (var f = 0; f < frames; f++)
        {
            var bob = walkCycle ? MathF.Sin(f / (float)frames * MathF.PI * 2f) * 2f : 0f;
            DrawCharacter(atlas, fw, fh, f, archetype, view, bob, shooting);
        }

        var id = registry.Register(atlas, fw * frames, fh, $"proc-{archetype}-{view}");
        var sheet = new TwoDSpriteSheet(id, fw, fh, fw * frames, fh);
        return new TwoDAnimationClip(sheet, Enumerable.Range(0, frames).ToArray(), fps);
    }

    private static ViewAngle ViewFromSuffix(string suffix) => suffix switch
    {
        "up" => ViewAngle.Up,
        "right" => ViewAngle.Right,
        "up_right" => ViewAngle.UpRight,
        "down_right" => ViewAngle.DownRight,
        _ => ViewAngle.Down,
    };

    private static void DrawCharacter(
        Rgba32[] atlas,
        int fw,
        int fh,
        int frame,
        DoomArchetype archetype,
        ViewAngle view,
        float bob,
        bool shooting)
    {
        var ox = frame * fw;
        var pal = PaletteFor(archetype);
        var cx = 24f;
        var cz = 28f + bob;
        var wide = archetype == DoomArchetype.Pinky ? 1.2f : 1f;

        FillEllipse(atlas, fw, fh, ox, cx, cz + 10f, 9f * wide, 4f, new Rgba32(0, 0, 0, 50));

        var (bodyRx, bodyRz, headOffX, headOffZ, gunX, gunZ) = LayoutForView(view, wide);
        FillEllipse(atlas, fw, fh, ox, cx, cz, bodyRx, bodyRz, pal.Body);
        FillEllipse(atlas, fw, fh, ox, cx + headOffX, cz + headOffZ, 5.5f * wide, 5f, pal.Skin);
        FillEllipse(atlas, fw, fh, ox, cx + headOffX, cz + headOffZ, 3.5f, 3f, pal.Highlight);

        switch (archetype)
        {
            case DoomArchetype.Marine:
                FillRect(atlas, fw, fh, ox, (int)(cx + gunX), (int)(cz + gunZ), 11, 4, pal.Gun);
                if (shooting)
                {
                    FillRect(atlas, fw, fh, ox, (int)(cx + gunX + 8), (int)(cz + gunZ - 1), 6, 2, new Rgba32(255, 240, 120));
                }
                FillRect(atlas, fw, fh, ox, (int)(cx - 4), (int)(cz - 2), 4, 8, pal.Accent);
                break;
            case DoomArchetype.Zombie:
                FillRect(atlas, fw, fh, ox, (int)(cx - 6), (int)(cz + 4), 5, 10, pal.Gore);
                FillRect(atlas, fw, fh, ox, (int)(cx + gunX), (int)(cz + 2), 7, 3, pal.Gore);
                break;
            case DoomArchetype.Imp:
                Set(atlas, fw, fh, ox, (int)(cx + headOffX - 4), (int)(cz + headOffZ - 5), pal.Horn);
                Set(atlas, fw, fh, ox, (int)(cx + headOffX + 4), (int)(cz + headOffZ - 5), pal.Horn);
                FillEllipse(atlas, fw, fh, ox, cx + gunX, cz + gunZ, 5, 4, pal.Accent);
                break;
            case DoomArchetype.Pinky:
                FillEllipse(atlas, fw, fh, ox, cx - 7, cz + 6, 5, 5, pal.Gore);
                FillEllipse(atlas, fw, fh, ox, cx + 7, cz + 6, 5, 5, pal.Gore);
                break;
        }
    }

    private static (float BodyRx, float BodyRz, float HeadOffX, float HeadOffZ, float GunX, float GunZ) LayoutForView(
        ViewAngle view,
        float wide)
    {
        return view switch
        {
            ViewAngle.Up => (8f * wide, 10f, 0f, -8f, 0f, -12f),
            ViewAngle.Down => (10f * wide, 11f, 0f, 6f, 0f, 10f),
            ViewAngle.Right => (7f * wide, 12f, 7f, 0f, 14f, 2f),
            ViewAngle.UpRight => (9f * wide, 10f, 5f, -5f, 10f, -4f),
            ViewAngle.DownRight => (9f * wide, 10f, 5f, 5f, 10f, 8f),
            _ => (10f, 11f, 0f, 6f, 0f, 10f),
        };
    }

    private static (Rgba32 Body, Rgba32 Skin, Rgba32 Highlight, Rgba32 Gun, Rgba32 Gore, Rgba32 Accent, Rgba32 Horn) PaletteFor(
        DoomArchetype archetype) =>
        archetype switch
        {
            DoomArchetype.Marine => (
                new Rgba32(42, 78, 48),
                new Rgba32(220, 185, 140),
                new Rgba32(180, 210, 160),
                new Rgba32(55, 58, 68),
                new Rgba32(120, 20, 20),
                new Rgba32(70, 130, 210),
                new Rgba32(40, 40, 40)),
            DoomArchetype.Zombie => (
                new Rgba32(58, 92, 42),
                new Rgba32(150, 165, 95),
                new Rgba32(100, 130, 70),
                new Rgba32(70, 55, 45),
                new Rgba32(150, 35, 35),
                new Rgba32(45, 65, 38),
                new Rgba32(60, 50, 40)),
            DoomArchetype.Imp => (
                new Rgba32(150, 42, 52),
                new Rgba32(230, 95, 75),
                new Rgba32(255, 150, 70),
                new Rgba32(70, 25, 30),
                new Rgba32(110, 20, 25),
                new Rgba32(255, 200, 50),
                new Rgba32(50, 15, 15)),
            DoomArchetype.Pinky => (
                new Rgba32(140, 55, 85),
                new Rgba32(210, 110, 125),
                new Rgba32(240, 150, 165),
                new Rgba32(95, 38, 58),
                new Rgba32(175, 45, 55),
                new Rgba32(255, 110, 145),
                new Rgba32(75, 28, 38)),
            _ => (
                new Rgba32(120, 120, 120),
                new Rgba32(180, 180, 180),
                new Rgba32(220, 220, 220),
                new Rgba32(60, 60, 60),
                new Rgba32(120, 20, 20),
                new Rgba32(80, 80, 200),
                new Rgba32(40, 40, 40)),
        };

    private static Rgba32 PixelMedkit(int x, int y)
    {
        if (x is >= 10 and <= 21 && y is >= 8 and <= 23)
        {
            return new Rgba32(200, 40, 40);
        }

        if ((x is 14 or 15 or 16 or 17) && y is >= 10 and <= 21)
        {
            return new Rgba32(240, 240, 240);
        }

        if ((y is 14 or 15 or 16 or 17) && x is >= 12 and <= 19)
        {
            return new Rgba32(240, 240, 240);
        }

        return default;
    }

    private static Rgba32 PixelArmor(int x, int y) =>
        x is >= 9 and <= 22 && y is >= 10 and <= 22 && (x + y) % 3 == 0
            ? new Rgba32(70, 130, 220)
            : default;

    private static Rgba32 PixelAmmoBox(int x, int y)
    {
        if (x is >= 8 and <= 23 && y is >= 12 and <= 24)
        {
            return new Rgba32(90, 70, 40);
        }

        if (x is >= 12 and <= 19 && y is >= 14 and <= 18)
        {
            return new Rgba32(220, 200, 60);
        }

        return default;
    }

    private static Rgba32 PixelKey(int x, int y)
    {
        if (x is >= 14 and <= 22 && y is >= 10 and <= 16)
        {
            return new Rgba32(80, 140, 255);
        }

        if (x is >= 8 and <= 14 && y is >= 14 and <= 18)
        {
            return new Rgba32(120, 180, 255);
        }

        return default;
    }

    private static Rgba32 PixelExitPad(int x, int y) =>
        x is >= 6 and <= 25 && y is >= 14 and <= 26
            ? new Rgba32(40, 200, 80, (byte)(120 + (x + y) % 80))
            : default;

    private static Rgba32 PixelBarrel(int x, int y)
    {
        var dx = x - 16f;
        var dy = y - 18f;
        if (dx * dx + dy * dy > 11 * 11)
        {
            return default;
        }

        return y % 4 < 2 ? new Rgba32(220, 60, 30) : new Rgba32(160, 40, 20);
    }

    private static void Set(Rgba32[] atlas, int fw, int fh, int ox, int x, int y, Rgba32 color)
    {
        if (x < 0 || x >= fw || y < 0 || y >= fh)
        {
            return;
        }

        atlas[y * fw + ox + x] = color;
    }

    private static void FillRect(Rgba32[] atlas, int fw, int fh, int ox, int x, int y, int w, int h, Rgba32 color)
    {
        for (var py = y; py < y + h; py++)
        {
            for (var px = x; px < x + w; px++)
            {
                Set(atlas, fw, fh, ox, px, py, color);
            }
        }
    }

    private static void FillEllipse(
        Rgba32[] atlas,
        int fw,
        int fh,
        int ox,
        float cx,
        float cy,
        float rx,
        float ry,
        Rgba32 color)
    {
        var x0 = (int)(cx - rx);
        var x1 = (int)(cx + rx);
        var y0 = (int)(cy - ry);
        var y1 = (int)(cy + ry);
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var dx = (x - cx) / rx;
                var dy = (y - cy) / ry;
                if (dx * dx + dy * dy <= 1f)
                {
                    Set(atlas, fw, fh, ox, x, y, color);
                }
            }
        }
    }

    private enum DoomArchetype
    {
        Marine,
        Zombie,
        Imp,
        Pinky,
    }

    private enum ViewAngle
    {
        Down,
        Up,
        Right,
        UpRight,
        DownRight,
    }
}

internal enum PickupArtKind
{
    Health,
    Armor,
    Ammo,
    BlueKey,
    Exit,
    Barrel,
}
