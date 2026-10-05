using FrankMoat.Game;
using Novolis.Rendering.Backends.TwoD.Silk;

var game = new FrankMoatGame();
SilkTwoDGame.Run(
    "Frank Moat vs the Evil Undead — Minigun Range",
    1280,
    720,
    game.Initialize,
    game.Update);
