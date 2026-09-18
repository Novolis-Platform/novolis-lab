using Novolis.Raylib.Colors;
using Novolis.Raylib.Game;

RayGame.Run("Hello Game", 800, 600, ctx =>
{
    ctx.Clear(RaylibColors.RayWhite);
    ctx.Text("Hello from Novolis.Raylib.Game", 24, 24, 28, RaylibColors.DarkGray);
});
