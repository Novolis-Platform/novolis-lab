using System.Numerics;
using Novolis.Simulation.SpaceCombat;

namespace XFighter.Game;

public static class CombatSystem
{
    public static bool SegmentHitsSphere(Vector3 segStart, Vector3 segEnd, Vector3 center, float radius) =>
        CombatHits.SegmentHitsSphere(segStart, segEnd, center, radius);
}

internal sealed class LaserBolt
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Life;
    public bool Active;
    public bool FromPlayer = true;
}

internal sealed class Explosion
{
    public Vector3 Position;
    public float Life;
    public float MaxLife;
    public bool Active;
}
