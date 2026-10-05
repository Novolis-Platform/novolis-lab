namespace FrankMoat.Particles;

internal sealed class ParticleField
{
    public const int Capacity = 12000;

    private readonly Particle[] _items = new Particle[Capacity];
    private int _count;

    public int Count => _count;

    public ReadOnlySpan<Particle> Span => _items.AsSpan(0, _count);

    public void Clear() => _count = 0;

    public void Emit(in Particle particle)
    {
        if (_count >= Capacity)
        {
            _items[Random.Shared.Next(_count)] = WithLife(particle);
            return;
        }

        _items[_count++] = WithLife(particle);
    }

    public void Tick(float dt)
    {
        for (var i = _count - 1; i >= 0; i--)
        {
            ref var p = ref _items[i];
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                _items[i] = _items[--_count];
                continue;
            }

            if (!p.Sticky)
            {
                var drag = MathF.Exp(-p.Drag * dt);
                p.Velocity *= drag;
                p.Position += p.Velocity * dt;
                p.ElevationVelocity -= 9.2f * dt;
                p.Elevation = MathF.Max(0f, p.Elevation + p.ElevationVelocity * dt);
                if (p.Elevation <= 0f)
                {
                    p.ElevationVelocity *= -0.18f;
                    p.Velocity *= 0.55f;
                }
            }

            p.Rotation += p.Spin * dt;
        }
    }

    private static Particle WithLife(Particle particle)
    {
        if (particle.MaxLife > 0f)
        {
            particle.Life = particle.MaxLife;
        }

        return particle;
    }
}
