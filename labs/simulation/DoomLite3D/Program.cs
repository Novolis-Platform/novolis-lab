using DoomLite3D.Game;
using Novolis.Raylib.Game;

var game = new DoomLiteGame();
RayGame.Run("Doom Lite 3D", 1280, 720, game.Initialize, game.Update);
