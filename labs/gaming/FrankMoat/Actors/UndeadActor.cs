using System.Numerics;

namespace FrankMoat.Actors;

internal sealed class UndeadActor
{
    public required UndeadKind Kind { get; init; }

    public Vector3 Position { get; set; }

    public Vector3 Facing { get; set; } = new(0f, 0f, -1f);

    public int Health { get; set; }

    public float Radius { get; init; }

    public float Speed { get; init; }

    public float AttackTimer { get; set; }

    public float HitFlash { get; set; }

    public static UndeadActor Create(UndeadKind kind, Vector3 position) => kind switch
    {
        UndeadKind.Runner => new UndeadActor
        {
            Kind = kind,
            Position = position,
            Health = 36,
            Radius = 0.32f,
            Speed = 4.6f,
        },
        UndeadKind.Tank => new UndeadActor
        {
            Kind = kind,
            Position = position,
            Health = 220,
            Radius = 0.55f,
            Speed = 2.1f,
        },
        _ => new UndeadActor
        {
            Kind = kind,
            Position = position,
            Health = 48,
            Radius = 0.36f,
            Speed = 2.4f,
        },
    };
}
