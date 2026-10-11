using Novolis.Rendering.Planar;
using TopDownDoom.Game.ImportedDoom;

namespace TopDownDoom.Art;

internal sealed class CharacterArtLibrary
{
    public CharacterAnimationSet Player { get; private set; } = null!;
    public CharacterAnimationSet Fodder { get; private set; } = null!;
    public CharacterAnimationSet Imp { get; private set; } = null!;
    public CharacterAnimationSet Bruiser { get; private set; } = null!;
    public PlanarAnimationClip? Explosion { get; private set; }
    public string SourceLabel { get; private set; } = "built-in";

    public PlanarTextureId HealthIcon { get; private set; }
    public PlanarTextureId ArmorIcon { get; private set; }
    public PlanarTextureId AmmoIcon { get; private set; }
    public PlanarTextureId KeyIcon { get; private set; }
    public PlanarTextureId ExitIcon { get; private set; }
    public PlanarTextureId BarrelIcon { get; private set; }
    public ParticleTextures Particles { get; private set; } = null!;

    public void Initialize(PlanarTextureRegistry registry, string contentRoot)
    {
        var assets = Path.Combine(contentRoot, "Assets");
        if (SquareCharacterArtLoader.TryLoad(
                registry,
                Path.Combine(assets, "SquareCharacters"),
                out var player,
                out var fodder,
                out var imp,
                out var bruiser,
                out var squareExplosion,
                out var squareLabel))
        {
            Player = player;
            Fodder = fodder;
            Imp = imp;
            Bruiser = bruiser;
            Explosion = squareExplosion;
            SourceLabel = squareLabel;
        }
        else if (TryLoadTdsPack(registry, Path.Combine(assets, "TdsCharacters")))
        {
            // SourceLabel set inside
        }
        else
        {
            Player = ProceduralDoomSprites.CreateMarine(registry);
            Fodder = ProceduralDoomSprites.CreateZombie(registry);
            Imp = ProceduralDoomSprites.CreateImp(registry);
            Bruiser = ProceduralDoomSprites.CreateBruiser(registry);
            Explosion = ProceduralDoomSprites.CreateExplosionClip(registry);
            SourceLabel = "built-in 8-way sprites (fetch-fun-art.ps1 for hand-drawn CC0)";
        }

        if (Explosion is null)
        {
            Explosion = ProceduralDoomSprites.CreateExplosionClip(registry);
        }

        Particles = ParticleTextures.Create(registry);
        HealthIcon = ProceduralDoomSprites.CreatePickupIcon(registry, PickupArtKind.Health);
        ArmorIcon = ProceduralDoomSprites.CreatePickupIcon(registry, PickupArtKind.Armor);
        AmmoIcon = ProceduralDoomSprites.CreatePickupIcon(registry, PickupArtKind.Ammo);
        KeyIcon = ProceduralDoomSprites.CreatePickupIcon(registry, PickupArtKind.BlueKey);
        ExitIcon = ProceduralDoomSprites.CreatePickupIcon(registry, PickupArtKind.Exit);
        BarrelIcon = ProceduralDoomSprites.CreatePickupIcon(registry, PickupArtKind.Barrel);
    }

    private bool TryLoadTdsPack(PlanarTextureRegistry registry, string root)
    {
        if (!Directory.Exists(root))
        {
            return false;
        }

        var walkGun = FindAnimationFolder(root, "walk", "gun");
        var gunShot = FindAnimationFolder(root, "gun", "shot");
        var walkKnife = FindAnimationFolder(root, "walk", "knife");

        var playerWalk = walkGun is not null
            ? CharacterAtlasBuilder.TryBuildClipFromFolder(registry, walkGun, 12f)
            : null;
        if (playerWalk is null)
        {
            return false;
        }

        var playerShoot = gunShot is not null
            ? CharacterAtlasBuilder.TryBuildClipFromFolder(registry, gunShot, 14f)
            : null;

        Player = new CharacterAnimationSet(playerWalk, 0.55f, playerShoot);
        var fodderWalk = walkKnife is not null
            ? CharacterAtlasBuilder.TryBuildClipFromFolder(registry, walkKnife, 9f) ?? playerWalk
            : playerWalk;
        Fodder = new CharacterAnimationSet(fodderWalk, 0.5f);
        Imp = Player;
        Bruiser = Fodder;
        Explosion = ProceduralDoomSprites.CreateExplosionClip(registry);
        SourceLabel = "TDS pack";
        return true;
    }

    private static string? FindAnimationFolder(string root, params string[] tokens)
    {
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(dir).Replace('_', ' ').Replace('-', ' ');
            if (tokens.All(t => name.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                if (Directory.EnumerateFiles(dir, "*.png").Take(2).Any())
                {
                    return dir;
                }
            }
        }

        return null;
    }

    public CharacterAnimationSet ForRole(Design.MonsterRole role) => role switch
    {
        Design.MonsterRole.Bruiser => Bruiser,
        Design.MonsterRole.Projectile or Design.MonsterRole.Hitscan or Design.MonsterRole.Flier => Imp,
        _ => Fodder,
    };

    public void TryLoadImported(PlanarTextureRegistry registry, ImportedDoomPackage package)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(package);

        Player = TryBuildImportedSet(registry, package, "PLAY") ?? Player;
        Fodder = TryBuildImportedSet(registry, package, "POSS") ?? Fodder;
        Imp = TryBuildImportedSet(registry, package, "TROO") ?? Imp;
        Bruiser = TryBuildImportedSet(registry, package, "SARG") ?? Bruiser;
        SourceLabel = $"imported Doom sprites ({package.Manifest.ContentProfile})";
    }

    private static CharacterAnimationSet? TryBuildImportedSet(
        PlanarTextureRegistry registry,
        ImportedDoomPackage package,
        string prefix)
    {
        var directions = new DirectionalClips { WorldHalfHeight = 0.65f };
        var loaded = 0;
        foreach (var (suffix, frame) in new[]
        {
            ("down", "A1"),
            ("down_right", "A2A8"),
            ("right", "A3A7"),
            ("up_right", "A4A6"),
            ("up", "A5"),
        })
        {
            var asset = package.Manifest.Assets.FirstOrDefault(candidate =>
                string.Equals(candidate.Kind, "sprites", StringComparison.OrdinalIgnoreCase)
                && candidate.Name.StartsWith(prefix + frame, StringComparison.OrdinalIgnoreCase));
            if (asset is null || !File.Exists(package.GetAssetPath(asset)))
            {
                continue;
            }

            var texture = PlanarPngLoader.LoadPng(registry, package.GetAssetPath(asset));
            var info = registry.GetInfo(texture);
            var sheet = new PlanarSpriteSheet(texture, info.Width, info.Height, info.Width, info.Height);
            var clip = new PlanarAnimationClip(sheet, [0], 1f);
            directions.AddIdle(suffix, clip);
            directions.AddMove(suffix, clip);
            loaded++;
        }

        if (loaded == 0)
        {
            return null;
        }

        var fallback = directions.Select(0f, moving: false, shooting: false).Clip;
        return new CharacterAnimationSet(fallback, directions.WorldHalfHeight, facing: directions);
    }
}
