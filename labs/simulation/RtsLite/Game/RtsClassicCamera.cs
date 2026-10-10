using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Simulation.View;
using Input = Novolis.Raylib.Interact.Input;

namespace RtsLite.Game;

/// <summary>App-layer input wiring for <see cref="FixedAngleMapCamera"/> (WASD, middle-drag, edge scroll, wheel).</summary>
internal sealed class RtsClassicCamera
{
    private readonly FixedAngleMapCamera _rig = new();

    public Vector3 PanTarget
    {
        get => _rig.PanTarget;
        set => _rig.PanTarget = value;
    }

    public float Distance
    {
        get => _rig.Distance;
        set => _rig.Distance = value;
    }

    public void SnapTo(Vector3 worldPoint) => _rig.SnapTo(worldPoint);

    public void Update(RayGameContext ctx)
    {
        var dt = ctx.DeltaSeconds;
        var panSpeed = 14f * (_rig.Distance / 26f);

        var forward = _rig.GroundForward();
        var right = _rig.GroundRight();

        if (ctx.IsKeyDown(KeyboardKey.W))
            _rig.Pan(forward * (panSpeed * dt));
        if (ctx.IsKeyDown(KeyboardKey.S))
            _rig.Pan(-forward * (panSpeed * dt));
        if (ctx.IsKeyDown(KeyboardKey.A))
            _rig.Pan(-right * (panSpeed * dt));
        if (ctx.IsKeyDown(KeyboardKey.D))
            _rig.Pan(right * (panSpeed * dt));

        if (ctx.IsMouseDown(MouseButton.Middle))
        {
            var delta = ctx.MouseDelta;
            var dragScale = 0.028f * (_rig.Distance / 26f);
            _rig.Pan(-right * (delta.X * dragScale) + forward * (delta.Y * dragScale));
        }

        EdgeScroll(ctx, right, forward, panSpeed * dt);

        _rig.AdjustDistance(Input.GetMouseWheelMove() * -1.8f);
        ClampPan();
    }

    public ViewPose BuildViewPose() => _rig.BuildViewPose();

    public Vector3 ScreenToGround(Vector2 screen, int screenW, int screenH) =>
        _rig.ScreenToGround(new Vector3(screen.X, screen.Y, 0f), screenW, screenH);

    private void EdgeScroll(RayGameContext ctx, Vector3 right, Vector3 forward, float step)
    {
        var mouse = Input.GetMousePosition();
        const int margin = 28;
        if (mouse.X < margin)
            _rig.Pan(-right * step);
        if (mouse.X > ctx.Width - margin)
            _rig.Pan(right * step);
        if (mouse.Y < margin)
            _rig.Pan(forward * step);
        if (mouse.Y > ctx.Height - margin)
            _rig.Pan(-forward * step);
    }

    private void ClampPan()
    {
        var half = RtsArena.GridSize * RtsArena.CellSize * 0.5f;
        var margin = 2f;
        _rig.ClampPan(margin, half * 2f - margin, margin, half * 2f - margin);
    }
}
