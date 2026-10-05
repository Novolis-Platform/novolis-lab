using System.Numerics;
using FrankMoat.Actors;
using FrankMoat.Art;
using FrankMoat.Game;
using FrankMoat.Levels;
using FrankMoat.Particles;
using Novolis.Math.Geometry;
using Novolis.Rendering.TwoD;

namespace FrankMoat.Rendering;

internal sealed class RangePresenter
{
    private readonly RangeArt _art;
    private readonly List<TwoDSpriteInstance> _particles = [];
    private readonly List<TwoDSpriteInstance> _decals = [];
    private readonly List<TwoDSpriteInstance> _actors = [];
    private TwoDSpriteInstance? _frank;
    private TwoDSpriteInstance? _cone;
    private int _decalCursor;

    public RangePresenter(RangeArt art) => _art = art;

    public void BuildStatic(TwoDScene scene)
    {
        var s = RangeBounds.Size;
        scene.Sprites.Add(new TwoDSpriteInstance
        {
            Texture = _art.Floor,
            Tint = Rgba32.White,
            SortKey = 0,
            Transform =
            {
                Position = new Vector3(s * 0.5f, 0f, s * 0.5f),
                Scale = new Vector3(s, 1f, s),
            },
        });

        for (var lane = 8f; lane <= 42f; lane += 5f)
        {
            scene.Sprites.Add(new TwoDSpriteInstance
            {
                Texture = _art.White,
                Tint = new Rgba32(176, 142, 42, 90),
                SortKey = 2,
                Transform =
                {
                    Position = new Vector3(25f, 0f, lane),
                    Scale = new Vector3(46f, 1f, 0.08f),
                },
            });
        }

        scene.Sprites.Add(new TwoDSpriteInstance
        {
            Texture = _art.White,
            Tint = new Rgba32(176, 142, 42, 160),
            SortKey = 3,
            Transform =
            {
                Position = new Vector3(25f, 0f, 10.4f),
                Scale = new Vector3(18f, 1f, 0.16f),
            },
        });
    }

    public void Sync(TwoDScene scene, RangeWorld world)
    {
        SyncFrank(scene, world);
        SyncCone(scene, world);
        SyncActors(scene, world);
        SyncDecals(scene, world);
        SyncParticles(scene, world);
    }

    public void ClearDynamic(TwoDScene scene)
    {
        if (_frank is not null)
        {
            scene.Sprites.Remove(_frank);
            _frank = null;
        }

        if (_cone is not null)
        {
            scene.Sprites.Remove(_cone);
            _cone = null;
        }

        foreach (var sprite in _actors)
        {
            scene.Sprites.Remove(sprite);
        }

        foreach (var sprite in _particles)
        {
            scene.Sprites.Remove(sprite);
        }

        foreach (var sprite in _decals)
        {
            scene.Sprites.Remove(sprite);
        }

        _actors.Clear();
        _particles.Clear();
        _decals.Clear();
        _decalCursor = 0;
    }

    private void SyncFrank(TwoDScene scene, RangeWorld world)
    {
        _frank ??= Add(scene, _art.Frank, 50);
        var aim = MathF.Atan2(world.Frank.Facing.Y, world.Frank.Facing.X);
        _frank.Transform.Position = HeightProjection.Elevate(world.Frank.Position, 0.15f);
        _frank.Transform.Scale = new Vector3(0.95f, 1f, 0.95f);
        _frank.Transform.RotationY = -aim;
        _frank.Tint = world.Frank.IFrames > 0f && (int)(world.Frank.IFrames * 20f) % 2 == 0
            ? new Rgba32(255, 180, 180)
            : Rgba32.White;
        _frank.SortKey = 50 + (int)(world.Frank.Position.Z * 2f);
    }

    private void SyncCone(TwoDScene scene, RangeWorld world)
    {
        _cone ??= Add(scene, _art.FlashCone, 12);
        var aim = MathF.Atan2(world.Frank.Facing.Y, world.Frank.Facing.X);
        var muzzle = world.Juice.MuzzleTimer > 0f ? 1.35f : 1f;
        _cone.Texture = _art.FlashCone;
        _cone.Transform.Position = HeightProjection.Elevate(
            world.Frank.Position + new Vector3(world.Frank.Facing.X * 4.2f, 0f, world.Frank.Facing.Y * 4.2f),
            0.35f);
        _cone.Transform.Scale = new Vector3(6.4f, 1f, 9.2f);
        _cone.Transform.RotationY = -aim + MathF.PI * 0.5f;
        var a = (byte)Math.Clamp((int)(150 * muzzle), 40, 220);
        _cone.Tint = new Rgba32(255, 236, 200, a);
        _cone.SortKey = 12;
    }

    private void SyncActors(TwoDScene scene, RangeWorld world)
    {
        var needed = world.Undead.Count + LiveBarrels(world) + LivePickups(world);
        while (_actors.Count < needed)
        {
            _actors.Add(Add(scene, _art.White, 48));
        }

        var i = 0;
        foreach (var foe in world.Undead)
        {
            var sprite = _actors[i++];
            sprite.Texture = foe.Kind switch
            {
                UndeadKind.Runner => _art.Runner,
                UndeadKind.Tank => _art.Tank,
                _ => _art.Shambler,
            };
            sprite.Transform.Position = HeightProjection.Elevate(foe.Position, 0.12f);
            sprite.Transform.Scale = new Vector3(foe.Radius * 2.4f, 1f, foe.Radius * 2.4f);
            sprite.Transform.RotationY = -MathF.Atan2(foe.Facing.Y, foe.Facing.X);
            sprite.Tint = foe.HitFlash > 0f ? new Rgba32(255, 200, 200) : Rgba32.White;
            sprite.SortKey = 48 + (int)(foe.Position.Z * 2f);
        }

        foreach (var barrel in world.Barrels)
        {
            if (!barrel.Alive)
            {
                continue;
            }

            var sprite = _actors[i++];
            sprite.Texture = _art.Barrel;
            sprite.Transform.Position = HeightProjection.Elevate(barrel.Position, 0.2f);
            sprite.Transform.Scale = new Vector3(0.7f, 1f, 0.7f);
            sprite.Tint = Rgba32.White;
            sprite.SortKey = 46 + (int)(barrel.Position.Z * 2f);
        }

        foreach (var pickup in world.Pickups)
        {
            if (pickup.Taken)
            {
                continue;
            }

            var sprite = _actors[i++];
            sprite.Texture = _art.Crate;
            sprite.Transform.Position = HeightProjection.Elevate(pickup.Position, 0.08f);
            sprite.Transform.Scale = new Vector3(0.45f, 1f, 0.45f);
            sprite.Tint = pickup.Kind switch
            {
                PickupKind.Health => new Rgba32(120, 220, 130),
                PickupKind.Rotary => new Rgba32(220, 200, 90),
                PickupKind.Pump12 => new Rgba32(200, 160, 80),
                _ => new Rgba32(180, 160, 120),
            };
            sprite.SortKey = 44 + (int)(pickup.Position.Z * 2f);
        }

        for (; i < _actors.Count; i++)
        {
            _actors[i].Transform.Scale = Vector3.Zero;
        }
    }

    private void SyncDecals(TwoDScene scene, RangeWorld world)
    {
        var span = world.Decals.Span;
        while (_decals.Count < span.Length)
        {
            _decals.Add(Add(scene, _art.Blood, 8));
        }

        for (var i = _decalCursor; i < span.Length; i++)
        {
            ApplyDecal(_decals[i], span[i]);
        }

        _decalCursor = span.Length;
        if (span.Length < _decals.Count && world.Decals.Count < DecalField.Capacity)
        {
            for (var i = 0; i < span.Length; i++)
            {
                ApplyDecal(_decals[i], span[i]);
            }

            for (var i = span.Length; i < _decals.Count; i++)
            {
                _decals[i].Transform.Scale = Vector3.Zero;
            }

            _decalCursor = span.Length;
        }
    }

    private void ApplyDecal(TwoDSpriteInstance sprite, in Decal decal)
    {
        sprite.Texture = decal.Kind switch
        {
            DecalKind.Scorch => _art.Smoke,
            DecalKind.SparkMark => _art.SoftGlow,
            _ => _art.Blood,
        };
        sprite.Tint = decal.Color;
        sprite.Transform.Position = HeightProjection.Elevate(decal.Position, decal.Elevation);
        sprite.Transform.Scale = new Vector3(decal.Width, 1f, decal.Height);
        sprite.Transform.RotationY = decal.Rotation;
        sprite.SortKey = 8;
    }

    private void SyncParticles(TwoDScene scene, RangeWorld world)
    {
        var span = world.Particles.Span;
        while (_particles.Count < span.Length)
        {
            _particles.Add(Add(scene, _art.SoftGlow, 80));
        }

        for (var i = 0; i < span.Length; i++)
        {
            var p = span[i];
            var t = p.MaxLife <= 0f ? 1f : 1f - p.Life / p.MaxLife;
            var sprite = _particles[i];
            sprite.Texture = p.Kind switch
            {
                ParticleKind.Spark => _art.Spark,
                ParticleKind.Smoke => _art.Smoke,
                ParticleKind.Shell => _art.Shell,
                ParticleKind.Blood => _art.Blood,
                ParticleKind.Debris => _art.Blood,
                ParticleKind.Ember => _art.SoftGlow,
                _ => _art.SoftGlow,
            };
            sprite.Tint = Lerp(p.ColorStart, p.ColorEnd, t);
            sprite.Transform.Position = HeightProjection.Elevate(p.Position, p.Elevation);
            var size = float.Lerp(p.SizeStart, p.SizeEnd, t);
            sprite.Transform.Scale = new Vector3(size, 1f, size);
            sprite.Transform.RotationY = p.Rotation;
            sprite.SortKey = 80 + (int)(p.Elevation * 10f);
        }

        for (var i = span.Length; i < _particles.Count; i++)
        {
            _particles[i].Transform.Scale = Vector3.Zero;
        }
    }

    private static TwoDSpriteInstance Add(TwoDScene scene, TwoDTextureId texture, int sort)
    {
        var sprite = new TwoDSpriteInstance
        {
            Texture = texture,
            SortKey = sort,
        };
        scene.Sprites.Add(sprite);
        return sprite;
    }

    private static int LiveBarrels(RangeWorld world)
    {
        var n = 0;
        foreach (var barrel in world.Barrels)
        {
            if (barrel.Alive)
            {
                n++;
            }
        }

        return n;
    }

    private static int LivePickups(RangeWorld world)
    {
        var n = 0;
        foreach (var pickup in world.Pickups)
        {
            if (!pickup.Taken)
            {
                n++;
            }
        }

        return n;
    }

    private static Rgba32 Lerp(Rgba32 a, Rgba32 b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Rgba32(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t));
    }
}
