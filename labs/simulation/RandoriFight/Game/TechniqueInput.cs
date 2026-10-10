using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;

namespace RandoriFight.Game;

internal static class TechniqueInput
{
    private const KeyboardKey KeyU = (KeyboardKey)'U';
    private const KeyboardKey KeyI = (KeyboardKey)'I';
    private const KeyboardKey KeyO = (KeyboardKey)'O';
    private const KeyboardKey KeyJ = (KeyboardKey)'J';
    private const KeyboardKey KeyK = (KeyboardKey)'K';
    private const KeyboardKey KeyL = (KeyboardKey)'L';

    public static bool ReadParryHeld(RayGameContext ctx) => ctx.IsKeyDown(KeyboardKey.H);

    public static FighterState? ReadAttackPressed(RayGameContext ctx)
    {
        if (ctx.IsKeyPressed(KeyU))
            return FighterState.Men;
        if (ctx.IsKeyPressed(KeyI))
            return FighterState.Kesa;
        if (ctx.IsKeyPressed(KeyO))
            return FighterState.Thrust;
        if (ctx.IsKeyPressed(KeyJ))
            return FighterState.Do;
        if (ctx.IsKeyPressed(KeyK))
            return FighterState.Kote;
        if (ctx.IsKeyPressed(KeyL))
            return FighterState.Kirioroshi;
        return null;
    }

    public static string ControlsHint =>
        "U men  I kesa  O tsuki  J do  K kote  L kirioroshi  |  H uke";
}
