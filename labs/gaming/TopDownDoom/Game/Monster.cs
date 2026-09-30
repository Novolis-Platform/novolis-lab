using System.Numerics;
using Novolis.Math.Geometry;
using TopDownDoom.Design;

namespace TopDownDoom.Game;

internal sealed class Monster(MonsterRole role, Vector3 position, int health)
{
    public MonsterRole Role = role;
    public Vector3 Position = position;
    public int Health = health;
    public TimeSpan FireCooldown;
}
