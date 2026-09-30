using System.Numerics;
using Novolis.Math.Geometry;
using TopDownDoom.Design;

namespace TopDownDoom.Game;

internal sealed class Pickup(PickupKind kind, Vector3 position)
{
    public PickupKind Kind = kind;
    public Vector3 Position = position;
}
