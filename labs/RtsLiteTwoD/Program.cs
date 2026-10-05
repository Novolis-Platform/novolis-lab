using Novolis.Rendering.TwoD;
using Novolis.Silk;
using RtsLiteTwoD.Game;

var game = new RtsLiteTwoDGame();
var scene = new TwoDScene();
SilkGame.Run(
    "RTS Lite TwoD — C&C top-down",
    1280,
    720,
    frame => game.Initialize(frame, scene),
    frame =>
    {
        scene.Menus.HandleInput(frame.IsMenuUpPressed(), frame.IsMenuDownPressed(), frame.IsMenuConfirmPressed(), frame.IsMenuCancelPressed());
        game.Update(frame, scene);
        frame.Submit(scene.Tessellate(frame.Width, frame.Height));
    });
