using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Rendering.TwoD;
using TopDownDoom.Art;

namespace TopDownDoom.Game;

internal sealed class FxPresenter(CharacterArtLibrary art)
{
    private const int ParticleSortBase = 180;

    public void Sync(TwoDScene scene, CombatJuice juice)
    {
        scene.Sprites.RemoveAll(s => s.SortKey is >= ParticleSortBase and < 220);
        scene.AnimatedSprites.RemoveAll(s => s.SortKey is >= 210 and < 215);

        var pool = juice.Particles.Particles;
        var n = pool.Count;
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

            scene.Sprites.Add(new TwoDSpriteInstance
            {
                Texture = tex,
                SortKey = ParticleSortBase + (int)(t * 30),
                Tint = color,
                Transform =
                {
                    Position = p.Position,
                    RotationY = p.Rotation,
                    Scale = new Vector3(size, 1f, size),
                },
            });
        }

        foreach (var fx in juice.SpriteBursts)
        {
            if (fx.Kind != CombatFxKind.ExplosionSprite || art.Explosion is null)
            {
                continue;
            }

            scene.AnimatedSprites.Add(new TwoDAnimatedSprite
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
