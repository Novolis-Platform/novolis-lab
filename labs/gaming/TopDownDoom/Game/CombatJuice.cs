using System.Numerics;

namespace TopDownDoom.Game;

internal enum CombatFxKind
{
    ExplosionSprite,
}

internal sealed class CombatFx(CombatFxKind kind, Vector3 position, float duration, float scale = 1f)
{
    public CombatFxKind Kind = kind;
    public Vector3 Position = position;
    public float Duration = duration;
    public float Time;
    public float Scale = scale;
}

internal sealed class CombatJuice
{
    public readonly ParticleSystem Particles = new();
    public readonly List<CombatFx> SpriteBursts = [];
    public float Shake;
    public float AmbientCooldown;

    public void Tick(float dt)
    {
        Shake = MathF.Max(0f, Shake - dt * 2.4f);
        AmbientCooldown -= dt;
        Particles.Tick(dt);
        for (var i = SpriteBursts.Count - 1; i >= 0; i--)
        {
            SpriteBursts[i].Time += dt;
            if (SpriteBursts[i].Time >= SpriteBursts[i].Duration)
            {
                SpriteBursts.RemoveAt(i);
            }
        }
    }

    public void AddShake(float amount) => Shake = MathF.Min(1.4f, Shake + amount);

    public void Boom(Vector3 position, float scale = 1f)
    {
        ParticlePresets.Explosion(Particles, position, scale);
        SpriteBursts.Add(new CombatFx(CombatFxKind.ExplosionSprite, position, 0.4f * scale, scale));
        AddShake(0.45f * scale);
    }

    public void Clear()
    {
        Particles.Clear();
        SpriteBursts.Clear();
        Shake = 0f;
    }
}
