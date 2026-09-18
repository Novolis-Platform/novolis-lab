using Novolis.Rendering.TwoD;

namespace TopDownDoom.Art;

/// <summary>
/// CC0 square characters — 8-way idle/jump; no Y-rotation.
/// https://opengameart.org/content/hand-drawn-square-characters-animated-8-directions-top-down-free-cc0
/// </summary>
internal static class SquareCharacterArtLoader
{
    public static bool TryLoad(
        TwoDTextureRegistry registry,
        string root,
        out CharacterAnimationSet player,
        out CharacterAnimationSet fodder,
        out CharacterAnimationSet imp,
        out CharacterAnimationSet bruiser,
        out TwoDAnimationClip? explosion,
        out string label)
    {
        player = null!;
        fodder = null!;
        imp = null!;
        bruiser = null!;
        explosion = null;
        label = string.Empty;

        var sprites = FindSpritesFolder(root);
        if (sprites is null)
        {
            return false;
        }

        var heroDir = Path.Combine(sprites, "Hero");
        var heroFacing = DirectionalArtLoader.TryLoadFolder(registry, heroDir, worldHalfHeight: 0.72f);
        if (heroFacing is null)
        {
            return false;
        }

        var fallback = heroFacing.Select(0f, false, false).Clip;
        player = new CharacterAnimationSet(fallback, heroFacing.WorldHalfHeight, facing: heroFacing);
        fodder = LoadRole(registry, Path.Combine(sprites, "Skeleton"), heroFacing, 0.66f) ?? player;
        imp = LoadRole(registry, Path.Combine(sprites, "Monster"), heroFacing, 0.6f) ?? player;
        bruiser = LoadRole(registry, Path.Combine(sprites, "Monster"), heroFacing, 0.82f) ?? imp;

        var deathFx = Path.Combine(sprites, "Death FX");
        explosion = CharacterAtlasBuilder.TryBuildClipFromFolder(registry, deathFx, 14f);
        label = "CC0 square characters (8-way)";
        return true;
    }

    private static CharacterAnimationSet? LoadRole(
        TwoDTextureRegistry registry,
        string folder,
        DirectionalClips fallbackFacing,
        float halfHeight)
    {
        var facing = DirectionalArtLoader.TryLoadFolder(registry, folder, halfHeight) ?? fallbackFacing;
        var clip = facing.Select(0f, false, false).Clip;
        return new CharacterAnimationSet(clip, facing.WorldHalfHeight, facing: facing);
    }

    private static string? FindSpritesFolder(string root)
    {
        if (!Directory.Exists(root))
        {
            return null;
        }

        var direct = Path.Combine(root, "Sprites");
        if (Directory.Exists(direct))
        {
            return direct;
        }

        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(dir).Equals("Sprites", StringComparison.OrdinalIgnoreCase))
            {
                return dir;
            }
        }

        return null;
    }
}
