using Novolis.Raylib.Game;
using RandoriFight.Game.Skeleton;

namespace RandoriFight.Game;

internal static class FighterRenderer
{
    private static bool _showSkeleton;

    public static void ToggleSkeletonOverlay() => _showSkeleton = !_showSkeleton;

    public static void Draw(RayGameContext ctx, Fighter fighter, bool isPlayer)
    {
        var skeleton = fighter.CurrentSkeleton;
        var hitFlash = fighter.State == FighterState.HitStun;
        var cutTrail = fighter.IsAttacking && fighter.MoveNormalizedTime is > 0.32f and < 0.68f;

        HumanoidMeshRenderer.Draw(
            ctx,
            skeleton,
            isPlayer,
            _showSkeleton,
            hitFlash,
            fighter.IsStrikeWindow,
            cutTrail);
    }
}
