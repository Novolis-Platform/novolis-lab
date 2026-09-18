using Novolis.Rendering.Backends.TwoD.Silk;
using TopDownDoom.Game;

var game = new TopDownDoomGame();
SilkTwoDGame.Run(
    "Top-Down Doom — lab (movement-first combat puzzle)",
    1024,
    768,
    game.Initialize,
    game.Update);
