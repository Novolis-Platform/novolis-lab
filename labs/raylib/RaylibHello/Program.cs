using Novolis.Raylib.Colors;
using Novolis.Raylib.Game;

RayGame.Run("Novolis Dogfood", 800, 600, ctx =>
{
    ctx.Clear(RaylibColors.RayWhite);
    ctx.Text("novolis-lab → novolis-raylib", 24, 24, 28, RaylibColors.DarkGray);
});
