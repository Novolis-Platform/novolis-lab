using Novolis.Raylib;
using Novolis.Raylib.Colors;
using Novolis.Raylib.Game;
using Novolis.Raylib.Rendering;

var guiReady = false;
RayGame.Run("Hello ImGui", 960, 540, ctx =>
{
    if (!guiReady)
    {
        Gui.Setup();
        guiReady = true;
    }

    ctx.Clear(RaylibColors.DarkGray);

    Gui.NewFrame();
    var open = true;
    if (Gui.BeginWindow("Demo", ref open))
    {
        if (Gui.Button("Click me"))
            ctx.Text("Button pressed!", 24, 24, 20, RaylibColors.RayWhite);
        Gui.Text("Dear ImGui via Novolis.Raylib");
    }

    Gui.EndWindow();
    Gui.Render();
});
