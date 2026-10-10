using Novolis.Raylib.Game;
using RagdollPlay.Game;

var game = new RagdollPlayGame();
RayGame.Run("Ragdoll Play", 1280, 720, game.Initialize, game.Update);
