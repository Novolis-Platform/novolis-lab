using Novolis.Rendering.Backends.TwoD.Silk;
using Novolis.Rendering.Presentation;

namespace FrankMoat.Input;

internal static class RangeControls
{
    public static RangeInput Read(SilkTwoDGameContext ctx)
    {
        var x = 0f;
        var z = 0f;
        if (ctx.IsKeyDown(Key.W) || ctx.IsKeyDown(Key.Up))
        {
            z += 1f;
        }

        if (ctx.IsKeyDown(Key.S) || ctx.IsKeyDown(Key.Down))
        {
            z -= 1f;
        }

        if (ctx.IsKeyDown(Key.A))
        {
            x -= 1f;
        }

        if (ctx.IsKeyDown(Key.D))
        {
            x += 1f;
        }

        var zoom = 0;
        if (ctx.IsKeyPressed(Key.Equal) || ctx.IsKeyPressed(Key.KeypadAdd))
        {
            zoom = -1;
        }
        else if (ctx.IsKeyPressed(Key.Minus) || ctx.IsKeyPressed(Key.KeypadSubtract))
        {
            zoom = 1;
        }

        var hotkey = WeaponHotkey.None;
        if (ctx.IsKeyPressed(Key.Number1))
        {
            hotkey = WeaponHotkey.One;
        }
        else if (ctx.IsKeyPressed(Key.Number2))
        {
            hotkey = WeaponHotkey.Two;
        }
        else if (ctx.IsKeyPressed(Key.Number3))
        {
            hotkey = WeaponHotkey.Three;
        }

        return new RangeInput(
            x,
            z,
            ctx.IsMouseButtonDown(MouseButton.Left),
            ctx.IsMouseButtonPressed(MouseButton.Left),
            ctx.IsKeyPressed(Key.R),
            ctx.IsKeyPressed(Key.H),
            ctx.IsKeyPressed(Key.E),
            hotkey,
            zoom);
    }
}
