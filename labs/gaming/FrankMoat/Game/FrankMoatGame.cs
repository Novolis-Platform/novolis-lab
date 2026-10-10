using System.Numerics;
using FrankMoat.Art;
using FrankMoat.Input;
using FrankMoat.Levels;
using FrankMoat.Rendering;
using Novolis.Game.Scenes;
using Novolis.Math.Geometry;
using Novolis.Silk;
using Novolis.Rendering.Planar;

namespace FrankMoat.Game;

internal sealed class FrankMoatGame
{
    private const float DefaultZoom = 0.016f;

    private readonly RangeWorld _world = new();
    private readonly RaisedWallDrawer _walls = new();
    private readonly FixedStepClock _clock = new();
    private readonly ViewportFollow _follow = new() { ShakeDecay = 0f };
    private RangeArt? _art;
    private RangePresenter? _presenter;
    private bool _playing;
    private bool _paused;
    private bool _ended;
    private float _zoom = DefaultZoom;

    public bool SkipMenu { get; init; }

    public void Initialize(SilkFrame frame, PlanarScene scene)
    {
        _art = RangeArt.Create(scene.Textures);
        _presenter = new RangePresenter(_art);
        scene.Camera.WorldUnitsPerPixel = _zoom;
        scene.Camera.ClearColor = new Rgba32(16, 18, 22);
        if (SkipMenu)
        {
            StartRun(scene);
            _playing = true;
            return;
        }
        scene.Menus.Push(new PlanarMenuScreen("FRANK MOAT", [
            new PlanarMenuItem("ENTER THE RANGE", Tag: "play", OnSelect: () =>
            {
                StartRun(scene);
                _playing = true;
                scene.Menus.Pop();
                return "play";
            }),
            new PlanarMenuItem("QUIT", Tag: "quit", OnSelect: () =>
            {
                Environment.Exit(0);
                return null;
            }),
        ]));
    }

    public void Update(SilkFrame frame, PlanarScene scene)
    {
        if (frame.IsKeyPressed(Key.Escape) && _playing && !_paused && !_world.Inspecting)
        {
            _paused = true;
            scene.Menus.Push(new PlanarMenuScreen("PAUSED", [
                new PlanarMenuItem("RESUME", OnSelect: () =>
                {
                    _paused = false;
                    scene.Menus.Pop();
                    return null;
                }),
                new PlanarMenuItem("QUIT", OnSelect: () =>
                {
                    Environment.Exit(0);
                    return null;
                }),
            ]));
        }

        if (!_playing || _paused || scene.Menus.IsActive)
        {
            return;
        }

        var input = RangeControls.Read(frame, scene);
        if (input.ZoomTicks != 0)
        {
            _zoom = Math.Clamp(_zoom + input.ZoomTicks * 0.0015f, 0.011f, 0.028f);
        }

        if (input.InspectPressed)
        {
            _world.Inspecting = !_world.Inspecting;
        }

        var aim = ViewportAim.DeltaXz(scene.Camera, frame.MousePosition.X, frame.MousePosition.Y, _world.Frank.Position);
        var look = aim.LengthSquaredXz() > 0.01f ? aim : _world.Frank.Facing;
        if (!_world.Inspecting)
        {
            var steps = _clock.Consume(frame.DeltaSeconds);
            for (var i = 0; i < steps; i++)
            {
                _world.Tick(_clock.StepSeconds, scene.Collision, input, aim);
            }
        }

        if (_world.Frank.Health <= 0)
        {
            ShowEnd(frame, scene, "FRANK IS DOWN");
            return;
        }

        if (_world.Cleared && !_ended)
        {
            ShowEnd(frame, scene, "RANGE CLEAR");
            return;
        }

        _follow.Shake = _world.Juice.Shake;
        _follow.Tick(scene.Camera, _world.Frank.Position, look, frame.DeltaSeconds);
        scene.Camera.WorldUnitsPerPixel = _zoom;
        scene.Camera.ClearColor = new Rgba32(
            (byte)(16 + _world.Juice.Shake * 18),
            18,
            22);
        _walls.TickOcclusion(_world.Frank.Position, _world.Juice.MuzzleTimer);
        _presenter?.Sync(scene, _world);
        scene.Update(frame.DeltaSeconds);
        RangeHud.Draw(scene, _world, _art!, frame.Width, _world.Inspecting);
        frame.SetTitle($"Frank Moat — FX {_world.Particles.Count}  wave {_world.Wave}");
    }

    private void StartRun(PlanarScene scene)
    {
        _presenter?.ClearDynamic(scene);
        _world.Reset();
        var walls = MinigunRange.Build(scene, _world);
        _presenter?.BuildStatic(scene);
        _walls.Build(scene, walls, _art!);
        _ended = false;
        _paused = false;
        _clock.Reset();
        _zoom = DefaultZoom;
        scene.Camera.Position = _world.Frank.Position;
        scene.Camera.WorldUnitsPerPixel = _zoom;
    }

    private void ShowEnd(SilkFrame frame, PlanarScene scene, string title)
    {
        if (_ended)
        {
            return;
        }

        _ended = true;
        _playing = false;
        scene.Menus.Push(new PlanarMenuScreen(title, [
            new PlanarMenuItem("AGAIN", OnSelect: () =>
            {
                StartRun(scene);
                _playing = true;
                scene.Menus.Pop();
                return null;
            }),
            new PlanarMenuItem("QUIT", OnSelect: () =>
            {
                Environment.Exit(0);
                return null;
            }),
        ]));
    }

}
