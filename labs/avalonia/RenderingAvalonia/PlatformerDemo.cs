using System.Numerics;
using Novolis.Avalonia.Rendering;
using Novolis.Math.Geometry;
using Novolis.Rendering.TwoD;

namespace RenderingAvalonia;

internal sealed class PlatformerDemo
{
    private readonly TwoDSceneControl _view;
    private readonly TwoDScene _scene;
    private Vector3 _player = Vector3PlanarExtensions.Xz(4f, 2f);
    private float _velocityZ;
    private TwoDStaticPolygon? _marker;

    public PlatformerDemo(TwoDSceneControl view)
    {
        _view = view;
        _scene = new TwoDScene();
        _view.Scene = _scene;
        _scene.Camera.ClearColor = new Rgba32(30, 36, 52);
        _scene.Camera.WorldUnitsPerPixel = 1f / 32f;
        _scene.AddPlatform(0f, 0f, 18f, 1.2f, new Rgba32(70, 90, 120));
        _scene.AddPlatform(4f, 3f, 8f, 3.8f, new Rgba32(90, 110, 150));
        _scene.AddPlatform(10f, 5.5f, 16f, 6.2f, new Rgba32(90, 110, 150));
        _view.FrameUpdating += OnFrame;
    }

    private void OnFrame(object? sender, TwoDFrameEventArgs e)
    {
        const float radius = 0.35f;
        var move = 0f;
        if (sender is TwoDSceneControl { IsFocused: true })
        {
            // keyboard handled on window level — demo uses simple auto-walk
        }

        move = MathF.Sin((float)Environment.TickCount64 * 0.001f) * 2f * e.DeltaSeconds;
        _player = _scene.Collision.MoveCircle(_player, new Vector3(move, 0f, 0f), radius);

        var grounded = !_scene.Collision.Overlaps(_player + new Vector3(0f, 0f, -(radius + 0.03f)), radius);
        if (!grounded)
        {
            _velocityZ -= 28f * e.DeltaSeconds;
            _player = _scene.Collision.MoveCircle(_player, new Vector3(0f, 0f, _velocityZ * e.DeltaSeconds), radius);
        }
        else if (_velocityZ < 0f)
        {
            _velocityZ = 0f;
        }

        _scene.Camera.Position = Vector3PlanarExtensions.Xz(_player.X, _player.Z + 1.5f);
        _marker = ReplaceMarker(_marker, _player, radius);
    }

    private TwoDStaticPolygon ReplaceMarker(TwoDStaticPolygon? prev, Vector3 pos, float radius)
    {
        if (prev is not null)
        {
            _scene.StaticPolygons.Remove(prev);
        }

        var r = radius;
        var marker = new TwoDStaticPolygon(
            TwoDScenePrimitives.Rectangle(pos.X - r, pos.Z - r, pos.X + r, pos.Z + r),
            new Rgba32(255, 180, 90))
        {
            DrawFilled = true,
            DrawOutline = true,
            SortKey = 1000,
        };
        _scene.StaticPolygons.Add(marker);
        return marker;
    }
}
