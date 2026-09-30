using System.Numerics;
using Novolis.Math.Geometry;
using TopDownDoom.Design;

namespace TopDownDoom.Game;

internal sealed class ExplosiveBarrel(Vector3 position)
{
    public Vector3 Position = position;
    public float Fuse;
}
