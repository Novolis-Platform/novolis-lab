using System.Numerics;
using Novolis.Lab.TwoD;
using Novolis.Math.Geometry;
using Novolis.Rendering.Backends.TwoD.Silk;
using Novolis.Rendering.TwoD;
using PlatformerHop.Game;
using Novolis.Rendering.Presentation;

namespace PlatformerTwoD.Game;

internal sealed class PlatformerTwoDGame
{
    private readonly PlanarHopPlayer _player = new();
    private readonly SideLevel _level = SideLevel.CreateDemo();
    private TwoDStaticPolygon? _playerMarker;
    private float _cameraX;

    public void Initialize(SilkTwoDGameContext ctx)
    {
        var scene = ctx.Scene;
        scene.Camera.ClearColor = new Rgba32(30, 36, 52);
        scene.Camera.WorldUnitsPerPixel = 1f / 32f;
        DenseGridPlatforms.AddSolidCells(scene, _level.Tiles, SideLevel.CellSize, new Rgba32(90, 110, 150));
        _player.Reset(_level);
        _cameraX = _player.Position.X;
    }

    public void Update(SilkTwoDGameContext ctx)
    {
        var scene = ctx.Scene;
        var move = 0f;
        if (ctx.IsKeyDown(Key.A))
        {
            move -= 1f;
        }

        if (ctx.IsKeyDown(Key.D))
        {
            move += 1f;
        }

        var jump = ctx.IsKeyPressed(Key.Space) || ctx.IsKeyPressed(Key.W);
        if (ctx.IsKeyPressed(Key.R))
        {
            _player.Reset(_level);
        }

        _player.Update(_level, move, jump, ctx.DeltaSeconds);

        var targetX = _player.Position.X;
        var t = 1f - MathF.Exp(-8f * ctx.DeltaSeconds);
        _cameraX = float.Lerp(_cameraX, targetX, t);
        scene.Camera.Position = Vector3PlanarExtensions.Xz(_cameraX, _player.Position.Z + 1.5f);

        scene.Update(ctx.DeltaSeconds);
        scene.Hud.Elements.Clear();
        scene.Hud.AddText("A/D move  |  Space/W jump  |  R reset", 12, 12, 2f, new Rgba32(210, 220, 235));
        scene.Hud.AddText(
            $"pos {_player.Position.X:F1},{_player.Position.Z:F1}  vz {_player.VelocityZ:F2}",
            12,
            36,
            2f,
            new Rgba32(180, 200, 220));

        _playerMarker = DenseGridPlatforms.ReplaceSquareMarker(
            scene,
            _playerMarker,
            _player.Position,
            SideLevel.PlayerRadius,
            new Rgba32(255, 180, 90));
    }
}
