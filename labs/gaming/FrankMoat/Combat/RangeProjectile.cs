using System.Numerics;

namespace FrankMoat.Combat;

internal sealed class RangeProjectile
{
    public Vector3 Position;
    public Vector3 Velocity;
    public int Damage;
    public float Life = 0.9f;
    public bool FromFrank = true;
}
