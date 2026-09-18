using System.Numerics;
using Novolis.Math.Geometry;

namespace TopDownDoom.Game;

internal sealed class ParticleSystem
{
    private const int MaxCount = 2800;

    public readonly List<Particle> Particles = [];

    public int Count => Particles.Count;

    public void Clear() => Particles.Clear();

    public void Tick(float dt)
    {
        for (var i = Particles.Count - 1; i >= 0; i--)
        {
            var p = Particles[i];
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                Particles.RemoveAt(i);
                continue;
            }

            var drag = MathF.Exp(-p.Drag * dt);
            p.Velocity *= drag;
            p.Position += p.Velocity * dt;
            p.Rotation += p.Spin * dt;
            Particles[i] = p;
        }
    }

    public void Emit(in Particle particle)
    {
        if (Particles.Count >= MaxCount)
        {
            Particles.RemoveAt(0);
        }

        var p = particle;
        if (p.MaxLife > 0f)
        {
            p.Life = p.MaxLife;
        }

        Particles.Add(p);
    }

    public void EmitMany(ReadOnlySpan<Particle> batch)
    {
        foreach (ref readonly var p in batch)
        {
            Emit(p);
        }
    }
}
