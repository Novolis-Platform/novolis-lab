using Novolis.Raylib.Game;
using XFighter.Game;

var game = new XFighterGame();
RayGame.Run("X-Fighter — Red Squadron", 1920, 1080, game.Initialize, game.Update);
