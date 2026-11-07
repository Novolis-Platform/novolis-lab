using System.Drawing;
using System.Numerics;

namespace ArtillerySimulator;

/// <summary>Lightweight spark / dust particle system drawn as short world lines.</summary>
internal sealed class ArtilleryParticles
{
    private readonly List<Particle> _live = new(256);
    private readonly Random _rng = new(0x51AF);

    public void SpawnMuzzle(Vector3 origin, Vector3 forward)
    {
        var dir = Vector3.Normalize(forward);
        for (var i = 0; i < 28; i++)
        {
            var spray = dir * (18f + (float)_rng.NextDouble() * 40f)
                + Lateral(dir) * ((float)_rng.NextDouble() * 2f - 1f) * 22f
                + Vector3.UnitY * ((float)_rng.NextDouble() * 18f);
            _live.Add(new Particle(
                origin,
                spray,
                life: 0.18f + (float)_rng.NextDouble() * 0.28f,
                size: 2.5f + (float)_rng.NextDouble() * 6f,
                hot: true));
        }
    }

    public void SpawnBurst(Vector3 origin, bool airburst)
    {
        var count = airburst ? 48 : 64;
        for (var i = 0; i < count; i++)
        {
            var theta = (float)_rng.NextDouble() * MathF.Tau;
            var phi = (float)_rng.NextDouble() * MathF.PI;
            var speed = airburst ? 35f + (float)_rng.NextDouble() * 55f : 25f + (float)_rng.NextDouble() * 70f;
            var vel = new Vector3(
                MathF.Sin(phi) * MathF.Cos(theta),
                MathF.Cos(phi) * (airburst ? 0.85f : 1.15f),
                MathF.Sin(phi) * MathF.Sin(theta)) * speed;
            _live.Add(new Particle(
                origin,
                vel,
                life: 0.55f + (float)_rng.NextDouble() * 1.1f,
                size: 4f + (float)_rng.NextDouble() * 14f,
                hot: i % 3 != 0));
        }

        // Dust ring
        for (var i = 0; i < 24; i++)
        {
            var a = i / 24f * MathF.Tau;
            var vel = new Vector3(MathF.Cos(a), 0.15f, MathF.Sin(a)) * (40f + (float)_rng.NextDouble() * 30f);
            _live.Add(new Particle(origin, vel, life: 1.4f, size: 10f, hot: false));
        }
    }

    public void Update(float dt)
    {
        for (var i = _live.Count - 1; i >= 0; i--)
        {
            var p = _live[i];
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                _live.RemoveAt(i);
                continue;
            }

            p.Velocity += new Vector3(0f, -18f, 0f) * dt;
            p.Velocity *= MathF.Exp(-1.6f * dt);
            p.Position += p.Velocity * dt;
            _live[i] = p;
        }
    }

    public void Draw(SilkWireBatch wires)
    {
        foreach (var p in _live)
        {
            var t = p.Age / p.Life;
            var fade = 1f - t;
            var alpha = (int)(255 * Math.Clamp(fade, 0f, 1f));
            var color = p.Hot
                ? Color.FromArgb(alpha, 255, (int)(180 + 60 * fade), (int)(80 + 40 * fade))
                : Color.FromArgb((int)(alpha * 0.7f), 160, 150, 130);
            var tip = p.Position + Vector3.Normalize(p.Velocity.LengthSquared() < 1e-4f
                ? Vector3.UnitY
                : p.Velocity) * (p.Size * fade);
            wires.Line(p.Position, tip, color);
        }
    }

    private static Vector3 Lateral(Vector3 dir)
    {
        var right = Vector3.Cross(dir, Vector3.UnitY);
        return right.LengthSquared() < 1e-6f ? Vector3.UnitZ : Vector3.Normalize(right);
    }

    private struct Particle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Age;
        public readonly float Life;
        public readonly float Size;
        public readonly bool Hot;

        public Particle(Vector3 position, Vector3 velocity, float life, float size, bool hot)
        {
            Position = position;
            Velocity = velocity;
            Age = 0f;
            Life = life;
            Size = size;
            Hot = hot;
        }
    }
}
