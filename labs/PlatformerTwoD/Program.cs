using Novolis.Rendering.TwoD;
using Novolis.Silk;
using PlatformerTwoD.Game;

var game = new PlatformerTwoDGame();
var scene = new TwoDScene();
SilkGame.Run(
    "Platformer TwoD (Simulation + Rendering.TwoD)",
    960,
    540,
    frame => game.Initialize(frame, scene),
    frame =>
    {
        scene.Menus.HandleInput(frame.IsMenuUpPressed(), frame.IsMenuDownPressed(), frame.IsMenuConfirmPressed(), frame.IsMenuCancelPressed());
        game.Update(frame, scene);
        frame.Submit(scene.Tessellate(frame.Width, frame.Height));
    });
