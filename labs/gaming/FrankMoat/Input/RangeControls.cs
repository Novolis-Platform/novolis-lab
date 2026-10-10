using Novolis.Game.Scenes;
using Novolis.Rendering.Planar;
using Novolis.Silk;

namespace FrankMoat.Input;

internal static class RangeControls
{
    public static RangeInput Read(SilkFrame frame, PlanarScene scene)
    {
        var move = PlanarMove.FromAxes(
            frame.IsKeyDown(Key.A),
            frame.IsKeyDown(Key.D),
            frame.IsKeyDown(Key.S) || frame.IsKeyDown(Key.Down),
            frame.IsKeyDown(Key.W) || frame.IsKeyDown(Key.Up));

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
            move.X,
            move.Z,
            frame.IsMouseButtonDown(MouseButton.Left),
            frame.IsMouseButtonPressed(MouseButton.Left),
            frame.IsKeyPressed(Key.R),
            frame.IsKeyPressed(Key.H),
            frame.IsKeyPressed(Key.E),
            hotkey,
            zoom);
    }
}
