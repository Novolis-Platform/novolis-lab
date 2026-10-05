using Novolis.Rendering.TwoD;
using Novolis.Silk;
using TopDownDoom.Game;

var game = new TopDownDoomGame();
var scene = new TwoDScene();
SilkGame.Run(
    "Top-Down Doom — lab (movement-first combat puzzle)",
    1024,
    768,
    frame => game.Initialize(frame, scene),
    frame =>
    {
        scene.Menus.HandleInput(frame.IsMenuUpPressed(), frame.IsMenuDownPressed(), frame.IsMenuConfirmPressed(), frame.IsMenuCancelPressed());
        game.Update(frame, scene);
        frame.Submit(scene.Tessellate(frame.Width, frame.Height));
    });
