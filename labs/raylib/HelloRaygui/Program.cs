using System.Drawing;
using Novolis.Raylib;
using Novolis.Raylib.Colors;
using Novolis.Raylib.Game;

RayGame.Run("Hello RayGui", 800, 600, ctx =>
{
    ctx.Clear(RaylibColors.RayWhite);
    RayGui.Enable();
    _ = RayGui.Panel(new RectangleF(32, 32, 320, 200), "RayGui add-on");
    RayGui.Label(new RectangleF(48, 80, 280, 24), "Optional rectangle widgets");
});
