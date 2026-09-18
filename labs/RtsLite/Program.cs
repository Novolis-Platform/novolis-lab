// Raylib pseudo-3D + PNG billboards. Prefer orthographic 2D: labs/RtsLiteTwoD
using Novolis.Raylib.Game;
using RtsLite.Game;

var game = new RtsLiteGame();
RayGame.Run("RTS Lite (Raylib)", 1280, 720, game.Initialize, game.Update);
