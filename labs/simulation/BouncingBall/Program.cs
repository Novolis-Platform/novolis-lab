using BouncingBall.Game;
using Novolis.Raylib.Game;

var game = new BouncingBallGame();
RayGame.Run("Bouncing Ball", 960, 720, game.Initialize, game.Update);
