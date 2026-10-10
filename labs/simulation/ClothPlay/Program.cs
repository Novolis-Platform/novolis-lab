using ClothPlay.Game;
using Novolis.Raylib.Game;

if (args.Any(a => a is "--smoke" or "-smoke"))
{
    var code = ClothSmoke.Run();
    Environment.Exit(code);
    return;
}

var game = new ClothPlayGame();
RayGame.Run("Cloth Play", 1280, 720, game.Initialize, game.Update);
