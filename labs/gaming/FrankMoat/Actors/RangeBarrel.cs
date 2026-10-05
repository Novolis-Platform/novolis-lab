using System.Numerics;

namespace FrankMoat.Actors;

internal sealed class RangeBarrel
{
    public required Vector3 Position { get; init; }

    public int Health { get; set; } = 18;

    public bool Alive => Health > 0;
}
