using Novolis.Raylib.Game;
using RandoriFight.Game;

var game = new RandoriFightGame();
RayGame.Run("Katana Randori", 1280, 720, game.Initialize, game.Update);
