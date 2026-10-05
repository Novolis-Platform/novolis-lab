using Novolis.Rendering.TwoD;
using Novolis.Silk;
using TapDuelFootball.Game;

var game = new TapDuelFootballGame();
var scene = new TwoDScene();
SilkGame.Run(
    "Tap Duel Football — Novolis",
    432,
    768,
    frame => game.Initialize(frame, scene),
    frame =>
    {
        scene.Menus.HandleInput(frame.IsMenuUpPressed(), frame.IsMenuDownPressed(), frame.IsMenuConfirmPressed(), frame.IsMenuCancelPressed());
        game.Update(frame, scene);
        frame.Submit(scene.Tessellate(frame.Width, frame.Height));
    });
