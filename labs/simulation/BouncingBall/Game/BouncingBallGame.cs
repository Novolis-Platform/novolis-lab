using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;

namespace BouncingBall.Game;

internal sealed class BouncingBallGame
{
    private const int WireframeBallLimit = 48;

    private static readonly Color Background = Color.FromArgb(255, 18, 22, 32);
    private static readonly Color WallWire = Color.FromArgb(255, 70, 90, 120);
    private static readonly Color HudText = Color.FromArgb(255, 210, 220, 235);

    private readonly DiagnosticsOverlay _diagnostics = new();

    private RoomWorld _room = null!;
    private BallWorld _balls = null!;
    private FixedRoomCamera _camera = null!;

    public void Initialize(RayGameContext ctx)
    {
        _room = RoomWorld.Create();
        _balls = new BallWorld(_room.CollisionWorld);
        _balls.BindRoom(_room);
        _balls.SpawnBall(_room);
        _camera = new FixedRoomCamera(_room.RoomCenter);
    }

    public void Update(RayGameContext ctx)
    {
        HandleSpawnInput(ctx);

        if (ctx.IsKeyPressed(KeyboardKey.R))
            _balls.ClearAndSpawnOne(_room);

        _diagnostics.ToggleIfKeyPressed(ctx);

        _balls.Step(ctx.DeltaSeconds);

        ctx.Clear(Background);
        var camera = _camera.BuildRaylibCamera();
        ctx.BeginWorld(camera);
        DrawWalls(ctx);
        DrawBalls(ctx);
        ctx.EndWorld();

        ctx.Text(
            "B +1  |  Ctrl+B +10  |  Ctrl+Shift+B +100  |  R reset  |  F3 diag",
            16,
            16,
            18,
            HudText);
        _diagnostics.Draw(ctx, AppendDiagnostics);
    }

    private void AppendDiagnostics(FrameDiagnostics _, IList<string> lines)
    {
        var grounded = 0;
        var speedSum = 0f;
        var speedMax = 0f;
        foreach (var sphere in _balls.Spheres)
        {
            if (sphere.IsGrounded)
                grounded++;
            speedSum += sphere.Speed;
            speedMax = MathF.Max(speedMax, sphere.Speed);
        }

        var avgSpeed = _balls.BallCount > 0 ? speedSum / _balls.BallCount : 0f;
        lines.Add($"balls {_balls.BallCount}  active {_balls.ActiveBallCount}  sleep {_balls.SleepingBallCount}");
        lines.Add($"grounded {grounded}  avgSpd {avgSpeed:F2}  maxSpd {speedMax:F2}");
        lines.Add($"contacts {_balls.BallBallContactsLastFrame}  pairs {_balls.BallBallPairChecksLastFrame}");
        lines.Add($"refl {_balls.IntegratorReflectionsLastFrame}  sub {_balls.PhysicsSubStepsLastFrame}  iters {_balls.BallBallSolveIterationsLastFrame}");
        if (_balls.BallBallSkippedLastFrame)
            lines.Add("sphere contact skipped (budget)");
        if (_balls.ClampedBallsLastFrame > 0)
            lines.Add($"clamped {_balls.ClampedBallsLastFrame}");
    }

    private void HandleSpawnInput(RayGameContext ctx)
    {
        if (!ctx.IsKeyPressed(KeyboardKey.B))
            return;

        var ctrl = ctx.IsKeyDown(KeyboardKey.LeftControl);
        var shift = ctx.IsKeyDown(KeyboardKey.LeftShift);
        if (ctrl && shift)
            _balls.SpawnBalls(_room, 100);
        else if (ctrl)
            _balls.SpawnBalls(_room, 10);
        else
            _balls.SpawnBall(_room);
    }

    private void DrawBalls(RayGameContext ctx)
    {
        var drawWires = _balls.BallCount <= WireframeBallLimit;
        for (var i = 0; i < _balls.BallCount; i++)
        {
            var sphere = _balls.Spheres[i];
            var (fill, wire) = BallColors.ForIndex(i);
            ctx.DrawGlowSphere(sphere.Position, SphereRadius.Value, fill);
            if (drawWires)
                ctx.DrawGlowSphereWires(sphere.Position, SphereRadius.Value, wire);
        }
    }

    private void DrawWalls(RayGameContext ctx)
    {
        var h = RoomWorld.WallHeight * 0.5f;
        for (var y = 0u; y < RoomWorld.GridSize; y++)
        for (var x = 0u; x < RoomWorld.GridSize; x++)
        {
            if (_room.Walls[x, y, 0] == 0)
                continue;

            var cx = (x + 0.5f) * RoomWorld.CellSize;
            var cz = (y + 0.5f) * RoomWorld.CellSize;
            ctx.DrawShipWires(
                new Vector3(cx, h, cz),
                new Vector3(RoomWorld.CellSize, RoomWorld.WallHeight, RoomWorld.CellSize),
                WallWire);
        }
    }
}
