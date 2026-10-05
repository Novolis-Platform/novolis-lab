using System.Numerics;

namespace FrankMoat.Combat;

internal readonly record struct WallHit(Vector3 Point, Vector3 Normal, float Distance);
