using System.Numerics;
using Novolis.Rendering.TwoD;
using TopDownDoom.Art;
using TopDownDoom.Design;

namespace TopDownDoom.Game;

internal sealed class SpriteScenePresenter(CharacterArtLibrary art)
{
    private readonly Dictionary<Monster, TwoDAnimatedSprite> _monsterSprites = new();
    private TwoDAnimatedSprite? _playerSprite;
    private float _shootDisplayTimer;

    public void Sync(TwoDScene scene, TopDownCombatWorld world, float dt)
    {
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

    public void Clear(TwoDScene scene)
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
    }

    private void SyncPlayer(TwoDScene scene, TopDownCombatWorld world)
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

    private void SyncMonsters(TwoDScene scene, TopDownCombatWorld world)
    {
        var live = new HashSet<Monster>(world.Monsters);
        foreach (var pair in _monsterSprites.ToArray())
        {
            if (!live.Contains(pair.Key))
            {
                scene.AnimatedSprites.Remove(pair.Value);
                _monsterSprites.Remove(pair.Key);
            }
        }

        foreach (var monster in world.Monsters)
        {
            if (!_monsterSprites.TryGetValue(monster, out var sprite))
            {
                sprite = new TwoDAnimatedSprite { Loop = true, SortKey = 90 };
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
        TwoDScene scene,
        ref TwoDAnimatedSprite? sprite,
        CharacterAnimationSet set,
        Vector3 position,
        float facing,
        bool moving,
        bool shooting,
        int sortKey)
    {
        sprite ??= new TwoDAnimatedSprite { Loop = true, SortKey = sortKey };
        sprite.SortKey = sortKey;
        ApplyToSprite(sprite, set, position, facing, moving, shooting);
        if (!scene.AnimatedSprites.Contains(sprite))
        {
            scene.AnimatedSprites.Add(sprite);
        }
    }

    private static void ApplyToSprite(
        TwoDAnimatedSprite sprite,
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

    private void SyncPickups(TwoDScene scene, TopDownCombatWorld world)
    {
        scene.Sprites.RemoveAll(s => s.SortKey is >= 40 and < 60);
        foreach (var pickup in world.Pickups)
        {
            var tex = pickup.Kind switch
            {
                PickupKind.Health => art.HealthIcon,
                PickupKind.Armor => art.ArmorIcon,
                PickupKind.Ammo => art.AmmoIcon,
                PickupKind.BlueKey => art.KeyIcon,
                PickupKind.Exit => art.ExitIcon,
                _ => art.AmmoIcon,
            };
            AddWorldSprite(scene, pickup.Position, tex, 0.38f, 45);
        }
    }

    private void SyncBarrels(TwoDScene scene, TopDownCombatWorld world)
    {
        scene.Sprites.RemoveAll(s => s.SortKey is >= 50 and < 70);
        foreach (var barrel in world.Barrels)
        {
            AddWorldSprite(scene, barrel.Position, art.BarrelIcon, 0.42f, 55);
        }
    }

    private void SyncProjectiles(TwoDScene scene, TopDownCombatWorld world)
    {
        scene.Sprites.RemoveAll(s => s.SortKey is >= 70 and < 85);
        foreach (var shot in world.Projectiles)
        {
            var rocket = shot.SplashRadius > 1.5f;
            var size = rocket ? 0.22f : 0.14f;
            var tint = shot.FromPlayer
                ? new Novolis.Math.Geometry.Rgba32(255, 240, 120, 240)
                : new Novolis.Math.Geometry.Rgba32(255, 90, 90, 240);
            scene.Sprites.Add(new TwoDSpriteInstance
            {
                Texture = art.Particles.Spark,
                SortKey = rocket ? 84 : 80,
                Tint = tint,
                Transform =
                {
                    Position = shot.Position,
                    Scale = new Vector3(size, 1f, size),
                },
            });
        }
    }

    private static void AddWorldSprite(TwoDScene scene, Vector3 pos, TwoDTextureId tex, float halfSize, int sort)
    {
        scene.Sprites.Add(new TwoDSpriteInstance
        {
            Texture = tex,
            SortKey = sort,
            Transform =
            {
                Position = pos,
                Scale = new Vector3(halfSize, 1f, halfSize),
            },
        });
    }

    private static float Distance(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
