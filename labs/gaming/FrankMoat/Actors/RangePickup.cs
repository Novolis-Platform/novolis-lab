using System.Numerics;

namespace FrankMoat.Actors;

internal sealed class RangePickup
{
    public required PickupKind Kind { get; init; }

    public required Vector3 Position { get; init; }

    public bool Taken { get; set; }
}
