using System.Numerics;
using FrankMoat.Art;
using FrankMoat.Input;
using FrankMoat.Levels;
using FrankMoat.Rendering;
using Novolis.Math.Geometry;
using Novolis.Rendering.Backends.TwoD.Silk;
using Novolis.Rendering.Presentation;
using Novolis.Rendering.TwoD;

namespace FrankMoat.Game;

internal sealed class FrankMoatGame
{
    private const float Step = 1f / 60f;
    private const float DefaultZoom = 0.016f;

    private readonly RangeWorld _world = new();
    private readonly RaisedWallDrawer _walls = new();
    private RangeArt? _art;
    private RangePresenter? _presenter;
    private bool _playing;
    private bool _paused;
    private bool _ended;
    private float _accum;
    private float _zoom = DefaultZoom;

    public void Initialize(SilkTwoDGameContext ctx)
    {
        _art = RangeArt.Create(ctx.Scene.Textures);
        _presenter = new RangePresenter(_art);
        ctx.Scene.Camera.WorldUnitsPerPixel = _zoom;
        ctx.Scene.Camera.ClearColor = new Rgba32(16, 18, 22);
        ctx.Scene.Menus.Push(new TwoDMenuScreen("FRANK MOAT", [
            new TwoDMenuItem("ENTER THE RANGE", Tag: "play", OnSelect: () =>
            {
                StartRun(ctx.Scene);
                _playing = true;
                ctx.Scene.Menus.Pop();
                return "play";
            }),
            new TwoDMenuItem("QUIT", Tag: "quit", OnSelect: () =>
            {
                Environment.Exit(0);
                return null;
            }),
        ]));
    }

    public void Update(SilkTwoDGameContext ctx)
    {
        if (ctx.IsKeyPressed(Key.Escape) && _playing && !_paused && !_world.Inspecting)
        {
            _paused = true;
            ctx.Scene.Menus.Push(new TwoDMenuScreen("PAUSED", [
                new TwoDMenuItem("RESUME", OnSelect: () =>
                {
                    _paused = false;
                    ctx.Scene.Menus.Pop();
                    return null;
                }),
                new TwoDMenuItem("QUIT", OnSelect: () =>
                {
                    Environment.Exit(0);
                    return null;
                }),
            ]));
        }

        if (!_playing || _paused || ctx.Scene.Menus.IsActive)
        {
            return;
        }

        var input = RangeControls.Read(ctx);
        if (input.ZoomTicks != 0)
        {
            _zoom = Math.Clamp(_zoom + input.ZoomTicks * 0.0015f, 0.011f, 0.028f);
        }

        if (input.InspectPressed)
        {
            _world.Inspecting = !_world.Inspecting;
        }

        var aim = ReadAim(ctx, ctx.Scene);
        if (!_world.Inspecting)
        {
            _accum += ctx.DeltaSeconds;
            _accum = MathF.Min(_accum, 0.12f);
            while (_accum >= Step)
            {
                _world.Tick(Step, ctx.Scene.Collision, input, aim);
                _accum -= Step;
            }
        }

        if (_world.Frank.Health <= 0)
        {
            ShowEnd(ctx, "FRANK IS DOWN");
            return;
        }

        if (_world.Cleared && !_ended)
        {
            ShowEnd(ctx, "RANGE CLEAR");
            return;
        }

        FollowCamera(ctx.Scene, ctx.DeltaSeconds, aim);
        _walls.TickOcclusion(_world.Frank.Position, _world.Juice.MuzzleTimer);
        _presenter?.Sync(ctx.Scene, _world);
        ctx.Scene.Update(ctx.DeltaSeconds);
        RangeHud.Draw(ctx.Scene, _world, _art!, ctx.Width, _world.Inspecting);
        ctx.SetTitle($"Frank Moat — FX {_world.Particles.Count}  wave {_world.Wave}");
    }

    private void StartRun(TwoDScene scene)
    {
        _presenter?.ClearDynamic(scene);
        _world.Reset();
        var walls = MinigunRange.Build(scene, _world);
        _presenter?.BuildStatic(scene);
        _walls.Build(scene, walls);
        _ended = false;
        _paused = false;
        _accum = 0f;
        _zoom = DefaultZoom;
        scene.Camera.Position = _world.Frank.Position;
        scene.Camera.WorldUnitsPerPixel = _zoom;
    }

    private void ShowEnd(SilkTwoDGameContext ctx, string title)
    {
        if (_ended)
        {
            return;
        }

        _ended = true;
        _playing = false;
        ctx.Scene.Menus.Push(new TwoDMenuScreen(title, [
            new TwoDMenuItem("AGAIN", OnSelect: () =>
            {
                StartRun(ctx.Scene);
                _playing = true;
                ctx.Scene.Menus.Pop();
                return null;
            }),
            new TwoDMenuItem("QUIT", OnSelect: () =>
            {
                Environment.Exit(0);
                return null;
            }),
        ]));
    }

    private void FollowCamera(TwoDScene scene, float dt, Vector2 aim)
    {
        var look = aim.LengthSquared() > 0.01f ? Vector2.Normalize(aim) : _world.Frank.Facing;
        var target = _world.Frank.Position + new Vector3(look.X * 2.3f, 0f, look.Y * 2.3f);
        var shake = _world.Juice.Shake;
        if (shake > 0.01f)
        {
            target += new Vector3(
                (Random.Shared.NextSingle() - 0.5f) * 0.4f * shake,
                0f,
                (Random.Shared.NextSingle() - 0.5f) * 0.4f * shake);
        }

        var t = 1f - MathF.Exp(-10f * dt);
        scene.Camera.Position = Vector3.Lerp(scene.Camera.Position, target, t);
        scene.Camera.WorldUnitsPerPixel = _zoom;
        scene.Camera.ClearColor = new Rgba32(
            (byte)(16 + shake * 18),
            18,
            22);
    }

    private Vector2 ReadAim(SilkTwoDGameContext ctx, TwoDScene scene)
    {
        var world = scene.Camera.ScreenToWorld(ctx.MousePosition.X, ctx.MousePosition.Y);
        return new Vector2(world.X - _world.Frank.Position.X, world.Z - _world.Frank.Position.Z);
    }
}
