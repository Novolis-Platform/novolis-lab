namespace RandoriFight.Game;

internal static class FightArena
{
    public const float StageWidth = 18f;
    public const float StageDepth = 6f;
    public const float FloorY = 0f;
    public const float WallX = StageWidth * 0.5f - 0.4f;

    public static float ClampX(float x) => Math.Clamp(x, -WallX, WallX);
}
