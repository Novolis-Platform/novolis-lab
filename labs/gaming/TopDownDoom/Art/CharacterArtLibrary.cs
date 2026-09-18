using Novolis.Rendering.TwoD;

namespace TopDownDoom.Art;

internal sealed class CharacterArtLibrary
{
    public CharacterAnimationSet Player { get; private set; } = null!;
    public CharacterAnimationSet Fodder { get; private set; } = null!;
    public CharacterAnimationSet Imp { get; private set; } = null!;
    public CharacterAnimationSet Bruiser { get; private set; } = null!;
    public TwoDAnimationClip? Explosion { get; private set; }
    public string SourceLabel { get; private set; } = "built-in";

    public TwoDTextureId HealthIcon { get; private set; }
    public TwoDTextureId ArmorIcon { get; private set; }
    public TwoDTextureId AmmoIcon { get; private set; }
    public TwoDTextureId KeyIcon { get; private set; }
    public TwoDTextureId ExitIcon { get; private set; }
    public TwoDTextureId BarrelIcon { get; private set; }
    public ParticleTextures Particles { get; private set; } = null!;

    public void Initialize(TwoDTextureRegistry registry, string contentRoot)
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

    private bool TryLoadTdsPack(TwoDTextureRegistry registry, string root)
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
}
