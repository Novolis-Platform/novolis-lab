using System.Numerics;
using Novolis.Math.Geometry;
using TopDownDoom.Design;

namespace TopDownDoom.Game;

internal sealed class Projectile(
    Vector3 position,
    Vector3 velocity,
    int damage,
    float splashRadius,
    bool causesSelfDamage,
    bool fromPlayer)
{
    public Vector3 Position = position;
    public Vector3 Velocity = velocity;
    public int Damage = damage;
    public float SplashRadius = splashRadius;
    public bool CausesSelfDamage = causesSelfDamage;
    public bool FromPlayer = fromPlayer;
    public float Life = 4f;
}
