using System.Numerics;
using Novolis.Simulation.SpaceCombat;

namespace XFighter.Game;

public static class CombatSystem
{
    public static bool SegmentHitsSphere(Vector3 segStart, Vector3 segEnd, Vector3 center, float radius) =>
        CombatHits.SegmentHitsSphere(segStart, segEnd, center, radius);
}
