using Novolis.Rendering.Planar;
using Novolis.Silk;
using RtsLitePlanar.Game;

var game = new RtsLitePlanarGame();
var scene = new PlanarScene();
SilkGame.Run(
    "RTS Lite Planar — C&C top-down",
    1280,
    720,
    frame => game.Initialize(frame, scene),
    frame =>
    {
        scene.Menus.HandleInput(frame.IsMenuUpPressed(), frame.IsMenuDownPressed(), frame.IsMenuConfirmPressed(), frame.IsMenuCancelPressed());
        game.Update(frame, scene);
        frame.Submit(scene.Tessellate(frame.Width, frame.Height));
    });
