using ArtillerySimulator.Game;
using Novolis.Raylib.Game;

if (args.Contains("--agent"))
    return ArtilleryAgentSession.Run();

var game = new ArtillerySimulatorGame();
RayGame.Run("Artillery Simulator", 1920, 1080, game.Initialize, game.Update);
return 0;
