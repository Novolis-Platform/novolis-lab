using System.Numerics;
using Novolis.Math.Geometry;

namespace TopDownDoom.Game;

internal sealed class ParticleSystem
{
    private const int MaxCount = 2800;

    public readonly List<Particle> Particles = [];
    private int _replaceIndex;

    public int Count => Particles.Count;

    public void Clear()
    {
        Particles.Clear();
        _replaceIndex = 0;
    }

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
        var p = particle;
        if (p.MaxLife > 0f)
        {
            p.Life = p.MaxLife;
        }

        if (Particles.Count >= MaxCount)
        {
            Particles[_replaceIndex] = p;
            _replaceIndex = (_replaceIndex + 1) % MaxCount;
            return;
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
