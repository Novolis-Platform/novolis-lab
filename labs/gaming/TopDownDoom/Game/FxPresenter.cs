using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Rendering.Planar;
using TopDownDoom.Art;

namespace TopDownDoom.Game;

internal sealed class FxPresenter(CharacterArtLibrary art)
{
    private const int ParticleSortBase = 180;
    private readonly List<PlanarSpriteInstance> _particleSprites = [];

    public void Sync(PlanarScene scene, CombatJuice juice)
    {
        scene.AnimatedSprites.RemoveAll(s => s.SortKey is >= 210 and < 215);

        var pool = juice.Particles.Particles;
        var n = pool.Count;
        if (scene.ParticleSprites.Count != _particleSprites.Count)
        {
            scene.ParticleSprites.Clear();
            scene.ParticleSprites.AddRange(_particleSprites);
        }

        while (_particleSprites.Count < n)
        {
            var sprite = new PlanarSpriteInstance();
            _particleSprites.Add(sprite);
            scene.ParticleSprites.Add(sprite);
        }

        while (_particleSprites.Count > n)
        {
            var last = _particleSprites.Count - 1;
            scene.ParticleSprites.RemoveAt(last);
            _particleSprites.RemoveAt(last);
        }

        for (var i = 0; i < n; i++)
        {
            var p = pool[i];
            var t = 1f - p.Life / p.MaxLife;
            var size = float.Lerp(p.SizeStart, p.SizeEnd, t);
            var color = LerpColor(p.ColorStart, p.ColorEnd, t);
            var tex = p.Sprite switch
            {
                ParticleSprite.Spark => art.Particles.Spark,
                ParticleSprite.Smoke => art.Particles.Smoke,
                ParticleSprite.Shell => art.Particles.Shell,
                _ => art.Particles.SoftGlow,
            };

            var sprite = _particleSprites[i];
            sprite.Texture = tex;
            sprite.SortKey = ParticleSortBase + (int)(t * 30);
            sprite.Tint = color;
            sprite.Transform.Position = p.Position;
            sprite.Transform.RotationY = p.Rotation;
            sprite.Transform.Scale = new Vector3(size, 1f, size);
        }

        foreach (var fx in juice.SpriteBursts)
        {
            if (fx.Kind != CombatFxKind.ExplosionSprite || art.Explosion is null)
            {
                continue;
            }

            scene.AnimatedSprites.Add(new PlanarAnimatedSprite
            {
                Clip = art.Explosion,
                Loop = false,
                Time = fx.Time,
                SortKey = 212,
                Transform =
                {
                    Position = fx.Position,
                    Scale = new Vector3(fx.Scale * 1.6f, 1f, fx.Scale * 1.6f),
                },
            });
        }
    }

    private static Rgba32 LerpColor(Rgba32 a, Rgba32 b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Rgba32(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t));
    }
}
