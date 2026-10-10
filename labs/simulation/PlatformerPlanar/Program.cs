using Novolis.Rendering.Planar;
using Novolis.Silk;
using PlatformerPlanar.Game;

var game = new PlatformerPlanarGame();
var scene = new PlanarScene();
SilkGame.Run(
    "Platformer Planar (Simulation + Rendering.Planar)",
    960,
    540,
    frame => game.Initialize(frame, scene),
    frame =>
    {
        scene.Menus.HandleInput(frame.IsMenuUpPressed(), frame.IsMenuDownPressed(), frame.IsMenuConfirmPressed(), frame.IsMenuCancelPressed());
        game.Update(frame, scene);
        frame.Submit(scene.Tessellate(frame.Width, frame.Height));
    });
