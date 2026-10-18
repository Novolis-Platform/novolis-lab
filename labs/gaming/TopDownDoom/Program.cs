using Novolis.Agent.Surface;
using Novolis.Rendering.Planar;
using Novolis.Silk;
using TopDownDoom.Game;
using TopDownDoom.Game.Agent;

var game = new TopDownDoomGame();
var scene = new PlanarScene();
AgentSurface? agentSurface = null;
SilkGame.Run(
    "Top-Down Doom — lab (movement-first combat puzzle)",
    1024,
    768,
    frame =>
    {
        game.Initialize(frame, scene);
        agentSurface = AgentSurface.TryAttachFromEnvironment(
            new TopDownDoomAgentHost(game),
            TopDownDoomAgentContract.Definition);
    },
    frame =>
    {
        scene.Menus.HandleInput(frame.IsMenuUpPressed(), frame.IsMenuDownPressed(), frame.IsMenuConfirmPressed(), frame.IsMenuCancelPressed());
        game.Update(frame, scene);
        frame.Submit(scene.Tessellate(frame.Width, frame.Height));
    });

agentSurface?.DisposeAsync().AsTask().GetAwaiter().GetResult();
