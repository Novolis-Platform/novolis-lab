using System.Numerics;
using Novolis.Game.Scenes;
using Novolis.Math.Geometry;
using Novolis.Silk;
using Novolis.Rendering.Planar;
using PlatformerHop.Game;

namespace PlatformerPlanar.Game;

internal sealed class PlatformerPlanarGame
{
    private readonly PlanarHopPlayer _player = new();
    private readonly SideLevel _level = SideLevel.CreateDemo();
    private PlanarStaticPolygon? _playerMarker;
    private float _cameraX;

    public void Initialize(SilkFrame frame, PlanarScene scene)
    {
        scene.Camera.ClearColor = new Rgba32(30, 36, 52);
        scene.Camera.WorldUnitsPerPixel = 1f / 32f;
        DenseGridPlatforms.AddSolidCells(scene, _level.Tiles, SideLevel.CellSize, new Rgba32(90, 110, 150));
        _player.Reset(_level);
        _cameraX = _player.Position.X;
    }

    public void Update(SilkFrame frame, PlanarScene scene)
    {
        var move = 0f;
        if (frame.IsKeyDown(Key.A))
        {
            move -= 1f;
        }

        if (frame.IsKeyDown(Key.D))
        {
            move += 1f;
        }

        var jump = frame.IsKeyPressed(Key.Space) || frame.IsKeyPressed(Key.W);
        if (frame.IsKeyPressed(Key.R))
        {
            _player.Reset(_level);
        }

        _player.Update(_level, move, jump, frame.DeltaSeconds);

        var targetX = _player.Position.X;
        var t = 1f - MathF.Exp(-8f * frame.DeltaSeconds);
        _cameraX = float.Lerp(_cameraX, targetX, t);
        scene.Camera.Position = Vector3PlanarExtensions.Xz(_cameraX, _player.Position.Z + 1.5f);

        scene.Update(frame.DeltaSeconds);
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
