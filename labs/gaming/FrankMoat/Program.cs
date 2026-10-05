using FrankMoat.Game;
using Novolis.Rendering.TwoD;
using Novolis.Silk;

var game = new FrankMoatGame();
var scene = new TwoDScene();
SilkGame.Run(
    "Frank Moat vs the Evil Undead — Minigun Range",
    1280,
    720,
    frame => game.Initialize(frame, scene),
    frame =>
    {
        scene.Menus.HandleInput(frame.IsMenuUpPressed(), frame.IsMenuDownPressed(), frame.IsMenuConfirmPressed(), frame.IsMenuCancelPressed());
        game.Update(frame, scene);
        frame.Submit(scene.Tessellate(frame.Width, frame.Height));
    });
