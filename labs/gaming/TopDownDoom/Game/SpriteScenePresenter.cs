using System.Numerics;
using Novolis.Rendering.Planar;
using TopDownDoom.Art;
using TopDownDoom.Design;

namespace TopDownDoom.Game;

internal sealed class SpriteScenePresenter(CharacterArtLibrary art)
{
    private readonly Dictionary<Monster, PlanarAnimatedSprite> _monsterSprites = new();
    private readonly HashSet<Monster> _liveMonsters = [];
    private readonly List<Monster> _staleMonsters = [];
    private readonly List<PlanarSpriteInstance> _pickupSprites = [];
    private readonly List<PlanarSpriteInstance> _barrelSprites = [];
    private readonly List<PlanarSpriteInstance> _projectileSprites = [];
    private PlanarAnimatedSprite? _playerSprite;
    private bool _spritePoolsAttached;
    private float _shootDisplayTimer;

    public void Sync(PlanarScene scene, TopDownCombatWorld world, float dt)
    {
        EnsureSpritePoolsAttached(scene);
        _shootDisplayTimer = MathF.Max(0f, _shootDisplayTimer - dt);
        if (world.FireCooldown > world.ActiveWeapon.FireInterval - TimeSpan.FromMilliseconds(120))
        {
            _shootDisplayTimer = 0.18f;
        }

        SyncPlayer(scene, world);
        SyncMonsters(scene, world);
        SyncPickups(scene, world);
        SyncBarrels(scene, world);
        SyncProjectiles(scene, world);
    }

    public void Clear(PlanarScene scene)
    {
        foreach (var sprite in _monsterSprites.Values)
        {
            scene.AnimatedSprites.Remove(sprite);
        }

        _monsterSprites.Clear();
        if (_playerSprite is not null)
        {
            scene.AnimatedSprites.Remove(_playerSprite);
            _playerSprite = null;
        }

        scene.Sprites.RemoveAll(s => s.SortKey is >= 30 and < 200);
        foreach (var sprite in _pickupSprites)
        {
            sprite.Enabled = false;
        }

        foreach (var sprite in _barrelSprites)
        {
            sprite.Enabled = false;
        }

        foreach (var sprite in _projectileSprites)
        {
            sprite.Enabled = false;
        }

        _spritePoolsAttached = false;
    }

    private void SyncPlayer(PlanarScene scene, TopDownCombatWorld world)
    {
        var moving = world.PlayerVelocity.LengthSquared() > 0.35f;
        var shooting = _shootDisplayTimer > 0f;
        ApplyCharacter(
            scene,
            ref _playerSprite,
            art.Player,
            world.PlayerPosition,
            world.PlayerFacingRadians,
            moving,
            shooting,
            sortKey: 120);
    }

    private void SyncMonsters(PlanarScene scene, TopDownCombatWorld world)
    {
        _liveMonsters.Clear();
        foreach (var monster in world.Monsters)
        {
            _liveMonsters.Add(monster);
        }

        _staleMonsters.Clear();
        foreach (var pair in _monsterSprites)
        {
            if (!_liveMonsters.Contains(pair.Key))
            {
                _staleMonsters.Add(pair.Key);
            }
        }

        foreach (var monster in _staleMonsters)
        {
            scene.AnimatedSprites.Remove(_monsterSprites[monster]);
            _monsterSprites.Remove(monster);
        }

        foreach (var monster in world.Monsters)
        {
            if (!_monsterSprites.TryGetValue(monster, out var sprite))
            {
                sprite = new PlanarAnimatedSprite { Loop = true, SortKey = 90 };
                _monsterSprites[monster] = sprite;
                scene.AnimatedSprites.Add(sprite);
            }

            var face = MathF.Atan2(
                world.PlayerPosition.Z - monster.Position.Z,
                world.PlayerPosition.X - monster.Position.X);
            var set = art.ForRole(monster.Role);
            var moving = Distance(monster.Position, world.PlayerPosition) > 1.2f;
            ApplyToSprite(sprite, set, monster.Position, face, moving, shooting: false);
            if (!scene.AnimatedSprites.Contains(sprite))
            {
                scene.AnimatedSprites.Add(sprite);
            }
        }
    }

    private void ApplyCharacter(
        PlanarScene scene,
        ref PlanarAnimatedSprite? sprite,
        CharacterAnimationSet set,
        Vector3 position,
        float facing,
        bool moving,
        bool shooting,
        int sortKey)
    {
        sprite ??= new PlanarAnimatedSprite { Loop = true, SortKey = sortKey };
        sprite.SortKey = sortKey;
        ApplyToSprite(sprite, set, position, facing, moving, shooting);
        if (!scene.AnimatedSprites.Contains(sprite))
        {
            scene.AnimatedSprites.Add(sprite);
        }
    }

    private static void ApplyToSprite(
        PlanarAnimatedSprite sprite,
        CharacterAnimationSet set,
        Vector3 position,
        float facingRadians,
        bool moving,
        bool shooting)
    {
        var (clip, flipX, halfHeight) = set.Resolve(facingRadians, moving, shooting);
        sprite.Clip = clip;
        sprite.Transform.Position = position;
        sprite.Transform.FlipX = flipX;
        sprite.Transform.RotationY = set.Facing is null ? facingRadians : 0f;
        var aspect = clip.Sheet.FrameWidth / (float)Math.Max(1, clip.Sheet.FrameHeight);
        sprite.Transform.Scale = new Vector3(halfHeight * aspect, 1f, halfHeight);
    }

    private void SyncPickups(PlanarScene scene, TopDownCombatWorld world)
    {
        EnsurePoolSize(scene, _pickupSprites, world.Pickups.Count, 45);
        for (var i = 0; i < _pickupSprites.Count; i++)
        {
            var sprite = _pickupSprites[i];
            if (i >= world.Pickups.Count)
            {
                sprite.Enabled = false;
                continue;
            }

            var pickup = world.Pickups[i];
            var tex = pickup.Kind switch
            {
                PickupKind.Health => art.HealthIcon,
                PickupKind.Armor => art.ArmorIcon,
                PickupKind.Ammo => art.AmmoIcon,
                PickupKind.BlueKey => art.KeyIcon,
                PickupKind.Exit => art.ExitIcon,
                _ => art.AmmoIcon,
            };
            ConfigureWorldSprite(sprite, pickup.Position, tex, 0.38f, 45);
        }
    }

    private void SyncBarrels(PlanarScene scene, TopDownCombatWorld world)
    {
        EnsurePoolSize(scene, _barrelSprites, world.Barrels.Count, 55);
        for (var i = 0; i < _barrelSprites.Count; i++)
        {
            var sprite = _barrelSprites[i];
            if (i >= world.Barrels.Count)
            {
                sprite.Enabled = false;
                continue;
            }

            ConfigureWorldSprite(sprite, world.Barrels[i].Position, art.BarrelIcon, 0.42f, 55);
        }
    }

    private void SyncProjectiles(PlanarScene scene, TopDownCombatWorld world)
    {
        EnsurePoolSize(scene, _projectileSprites, world.Projectiles.Count, 80);
        for (var i = 0; i < _projectileSprites.Count; i++)
        {
            var sprite = _projectileSprites[i];
            if (i >= world.Projectiles.Count)
            {
                sprite.Enabled = false;
                continue;
            }

            var shot = world.Projectiles[i];
            var rocket = shot.SplashRadius > 1.5f;
            var size = rocket ? 0.22f : 0.14f;
            var tint = shot.FromPlayer
                ? new Novolis.Math.Geometry.Rgba32(255, 240, 120, 240)
                : new Novolis.Math.Geometry.Rgba32(255, 90, 90, 240);
            ConfigureWorldSprite(sprite, shot.Position, art.Particles.Spark, size, rocket ? 84 : 80);
            sprite.Tint = tint;
        }
    }

    private void EnsureSpritePoolsAttached(PlanarScene scene)
    {
        if (_spritePoolsAttached)
        {
            return;
        }

        AttachPool(scene, _pickupSprites);
        AttachPool(scene, _barrelSprites);
        AttachPool(scene, _projectileSprites);
        _spritePoolsAttached = true;
    }

    private static void AttachPool(PlanarScene scene, List<PlanarSpriteInstance> pool)
    {
        foreach (var sprite in pool)
        {
            if (!scene.Sprites.Contains(sprite))
            {
                scene.Sprites.Add(sprite);
            }
        }
    }

    private static void EnsurePoolSize(
        PlanarScene scene,
        List<PlanarSpriteInstance> pool,
        int count,
        int sortKey)
    {
        while (pool.Count < count)
        {
            var sprite = new PlanarSpriteInstance
            {
                Enabled = false,
                SortKey = sortKey,
            };
            pool.Add(sprite);
            scene.Sprites.Add(sprite);
        }
    }

    private static void ConfigureWorldSprite(PlanarSpriteInstance sprite, Vector3 pos, PlanarTextureId tex, float halfSize, int sort)
    {
        sprite.Enabled = true;
        sprite.Texture = tex;
        sprite.SortKey = sort;
        sprite.Tint = Novolis.Math.Geometry.Rgba32.White;
        sprite.Transform.Position = pos;
        sprite.Transform.Scale = new Vector3(halfSize, 1f, halfSize);
    }

    private static float Distance(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
