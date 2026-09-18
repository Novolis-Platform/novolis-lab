using Novolis.Rendering.Backends.TwoD.Silk;
using PlatformerTwoD.Game;

var game = new PlatformerTwoDGame();
SilkTwoDGame.Run("Platformer TwoD (Simulation + Rendering.TwoD)", 960, 540, game.Initialize, game.Update);
