// Raylib pseudo-3D side view (legacy). Prefer orthographic 2D: labs/PlatformerTwoD
using Novolis.Raylib.Game;
using PlatformerHop.Game;

var game = new PlatformerHopGame();
RayGame.Run("Platformer Hop (Raylib)", 960, 540, game.Initialize, game.Update);
