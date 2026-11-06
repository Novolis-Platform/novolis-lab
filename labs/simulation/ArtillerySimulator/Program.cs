using ArtillerySimulator.Game;
using Novolis.Silk;

// Kristiansand artillery — Silk host only (no Raylib).
var game = new ArtillerySimulatorGame();
SilkGame.Run("Artillery Simulator — Kristiansand", 1920, 1080, game.Initialize, game.Update);
