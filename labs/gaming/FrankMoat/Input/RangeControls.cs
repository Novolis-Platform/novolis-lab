using Novolis.Silk;
using Novolis.Rendering.TwoD;

namespace FrankMoat.Input;

internal static class RangeControls
{
    public static RangeInput Read(SilkFrame frame, TwoDScene scene)
    {
        var x = 0f;
        var z = 0f;
        if (frame.IsKeyDown(Key.W) || frame.IsKeyDown(Key.Up))
        {
            z += 1f;
        }

        if (frame.IsKeyDown(Key.S) || frame.IsKeyDown(Key.Down))
        {
            z -= 1f;
        }

        if (frame.IsKeyDown(Key.A))
        {
            x -= 1f;
        }

        if (frame.IsKeyDown(Key.D))
        {
            x += 1f;
        }

        var zoom = 0;
        if (frame.IsKeyPressed(Key.Equal) || frame.IsKeyPressed(Key.KeypadAdd))
        {
            zoom = -1;
        }
        else if (frame.IsKeyPressed(Key.Minus) || frame.IsKeyPressed(Key.KeypadSubtract))
        {
            zoom = 1;
        }

        var hotkey = WeaponHotkey.None;
        if (frame.IsKeyPressed(Key.Number1))
        {
            hotkey = WeaponHotkey.One;
        }
        else if (frame.IsKeyPressed(Key.Number2))
        {
            hotkey = WeaponHotkey.Two;
        }
        else if (frame.IsKeyPressed(Key.Number3))
        {
            hotkey = WeaponHotkey.Three;
        }

        return new RangeInput(
            x,
            z,
            frame.IsMouseButtonDown(MouseButton.Left),
            frame.IsMouseButtonPressed(MouseButton.Left),
            frame.IsKeyPressed(Key.R),
            frame.IsKeyPressed(Key.H),
            frame.IsKeyPressed(Key.E),
            hotkey,
            zoom);
    }
}
