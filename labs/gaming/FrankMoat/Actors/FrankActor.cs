using System.Numerics;

namespace FrankMoat.Actors;

internal sealed class FrankActor
{
    public const float Radius = 0.38f;
    public const float Speed = 6.8f;

    public Vector3 Position { get; set; } = new(25f, 0f, 12f);

    public Vector2 Facing { get; set; } = new(0f, 1f);

    public int Health { get; set; } = 100;

    public float IFrames { get; set; }
}
