using Novolis.Rendering.Backends.TwoD.Silk;
using TapDuelFootball.Game;

var game = new TapDuelFootballGame();
SilkTwoDGame.Run(
    "Tap Duel Football — Novolis",
    432,
    768,
    game.Initialize,
    game.Update);
