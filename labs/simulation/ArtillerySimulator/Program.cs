using ArtillerySimulator.Game;
using Novolis.Raylib.Game;

var game = new ArtillerySimulatorGame();
RayGame.Run("Artillery Simulator", 1920, 1080, game.Initialize, game.Update);
