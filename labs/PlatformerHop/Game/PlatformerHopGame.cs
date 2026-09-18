using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Simulation.View;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace PlatformerHop.Game;

internal sealed class PlatformerHopGame
{
    private static readonly Color Background = Color.FromArgb(255, 30, 36, 52);
    private static readonly Color TileFill = Color.FromArgb(255, 90, 110, 150);
    private static readonly Color PlayerColor = Color.FromArgb(255, 255, 180, 90);
    private static readonly Color HudText = Color.FromArgb(255, 210, 220, 235);

    private readonly DiagnosticsOverlay _diagnostics = new();
    private readonly SidePlayer _player = new();

    private SideLevel _level = null!;
    private float _cameraX;

    public void Initialize(RayGameContext ctx)
    {
        _level = SideLevel.CreateDemo();
        _player.Reset(_level);
        _cameraX = _player.Position.X;
    }

    public void Update(RayGameContext ctx)
    {
        var move = 0f;
        if (ctx.IsKeyDown(KeyboardKey.A))
            move -= 1f;
        if (ctx.IsKeyDown(KeyboardKey.D))
            move += 1f;

        var jump = ctx.IsKeyPressed(KeyboardKey.Space) || ctx.IsKeyPressed(KeyboardKey.W);
        if (ctx.IsKeyPressed(KeyboardKey.R))
            _player.Reset(_level);

        _player.Update(_level, move, jump, ctx.DeltaSeconds);
        _diagnostics.ToggleIfKeyPressed(ctx);

        var targetX = _player.Position.X;
        var t = 1f - MathF.Exp(-8f * ctx.DeltaSeconds);
        _cameraX = float.Lerp(_cameraX, targetX, t);

        ctx.Clear(Background);
        var camera = BuildSideCamera(ctx);
        ctx.BeginWorld(camera);
        DrawLevel(ctx);
        ctx.DrawGlowSphere(_player.Position, SideLevel.PlayerRadius, PlayerColor);
        ctx.EndWorld();

        ctx.Text("A/D move  |  Space jump  |  R reset  |  F3 diag", 16, 16, 18, HudText);
        _diagnostics.Draw(ctx, (_, lines) =>
        {
            lines.Add($"pos {_player.Position.X:F1},{_player.Position.Y:F1}");
            lines.Add($"vy {_player.VelocityY:F2}");
        });
    }

    private RayCamera BuildSideCamera(RayGameContext ctx)
    {
        var eye = new Vector3(_cameraX, _player.Position.Y + 1.5f, 12f);
        var target = new Vector3(_cameraX, _player.Position.Y + 0.5f, 0f);
        var pose = new ViewPose(eye, target, Vector3.UnitY, 55f);
        return RayCamera.Perspective(pose.Position, pose.Target, pose.Up, pose.FieldOfViewDegrees);
    }

    private void DrawLevel(RayGameContext ctx)
    {
        var tiles = _level.Tiles;
        for (var y = 0u; y < tiles.Height; y++)
        for (var x = 0u; x < tiles.Width; x++)
        {
            if (tiles[x, y, 0] == 0)
                continue;

            var cx = (x + 0.5f) * SideLevel.CellSize;
            var cy = (y + 0.5f) * SideLevel.CellSize;
            ctx.DrawShipBox(new Vector3(cx, cy, 0f), new Vector3(SideLevel.CellSize, SideLevel.CellSize, 0.2f), TileFill);
        }
    }
}
