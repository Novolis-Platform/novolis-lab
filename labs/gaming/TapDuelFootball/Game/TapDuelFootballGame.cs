using System.Numerics;
using Novolis.Game.MenuFlows;
using Novolis.Math.Geometry;
using Novolis.Rendering.Backends.TwoD.Silk;
using Novolis.Rendering.Presentation;
using Novolis.Rendering.TwoD;
using TapDuelFootball.Art;

namespace TapDuelFootball.Game;

internal sealed class TapDuelFootballGame
{
    private readonly TapDuelMatch _match = new(FieldPainter.FieldHalfLength, stepSize: 0.55f);
    private readonly GameScreenStack _flows = new();
    private TwoDSpriteInstance? _ball;
    private Phase _phase = Phase.Title;
    private float _countdown;
    private float _postWinTimer;
    private int? _pendingWinner;

    private enum Phase
    {
        Title,
        Countdown,
        Playing,
        WinnerFlash,
        EndMenu,
    }

    public void Initialize(SilkTwoDGameContext ctx)
    {
        var scene = ctx.Scene;
        scene.Camera.ClearColor = new Rgba32(28, 72, 36);
        scene.Camera.Position = Vector3.Zero;
        FitCamera(ctx);

        FieldPainter.Paint(scene);

        var footballId = ProceduralFootball.Register(scene.Textures);
        _ball = new TwoDSpriteInstance
        {
            Texture = footballId,
            Layer = TwoDDrawLayer.World,
            SortKey = 100,
            Transform =
            {
                Position = Vector3.Zero,
                Scale = new Vector3(1.35f, 1f, 1.9f),
            },
        };
        scene.Sprites.Add(_ball);

        _ = _flows.PushAsync(new TitleFlowScreen());
        PushTitleMenu(ctx);
    }

    public void Update(SilkTwoDGameContext ctx)
    {
        FitCamera(ctx);
        var scene = ctx.Scene;
        scene.Hud.Elements.Clear();

        switch (_phase)
        {
            case Phase.Title:
                DrawStaticLabels(ctx);
                break;

            case Phase.Countdown:
                DrawStaticLabels(ctx);
                _countdown -= ctx.DeltaSeconds;
                var shown = MathF.Max(0f, _countdown);
                scene.Hud.AddText(
                    shown < 0.15f ? "GO!" : $"{MathF.Ceiling(shown)}",
                    ctx.Width * 0.5f - 28f,
                    ctx.Height * 0.5f - 24f,
                    5f,
                    new Rgba32(255, 255, 255));
                if (_countdown <= 0f)
                {
                    _phase = Phase.Playing;
                    _ = _flows.PushAsync(new PlayFlowScreen());
                }

                break;

            case Phase.Playing:
                DrawStaticLabels(ctx);
                HandlePlayInput(ctx);
                if (_match.IsFinished)
                {
                    _pendingWinner = _match.Winner;
                    _postWinTimer = 2.2f;
                    _phase = Phase.WinnerFlash;
                }

                break;

            case Phase.WinnerFlash:
                DrawStaticLabels(ctx);
                DrawWinnerBanner(ctx);
                _postWinTimer -= ctx.DeltaSeconds;
                if (_postWinTimer <= 0f)
                {
                    _phase = Phase.EndMenu;
                    PushEndMenu(ctx);
                }

                break;

            case Phase.EndMenu:
                DrawStaticLabels(ctx);
                DrawWinnerBanner(ctx);
                break;
        }

        UpdateBallSprite();
        scene.Update(ctx.DeltaSeconds);
    }

    private void HandlePlayInput(SilkTwoDGameContext ctx)
    {
        // Hotseat: bottom half / A / S / Down = Player 1; top half / W / Up = Player 2.
        if (ctx.IsKeyPressed(Key.A) || ctx.IsKeyPressed(Key.S) || ctx.IsKeyPressed(Key.Down))
        {
            _match.TapPlayer1();
        }

        if (ctx.IsKeyPressed(Key.W) || ctx.IsKeyPressed(Key.Up))
        {
            _match.TapPlayer2();
        }

        if (ctx.IsMouseButtonPressed(MouseButton.Left))
        {
            if (ctx.MousePosition.Y < ctx.Height * 0.5f)
            {
                _match.TapPlayer2();
            }
            else
            {
                _match.TapPlayer1();
            }
        }
    }

    private void UpdateBallSprite()
    {
        if (_ball is null)
        {
            return;
        }

        _ball.Transform.Position = Vector3PlanarExtensions.Xz(0f, _match.BallZ);
    }

    private void DrawStaticLabels(SilkTwoDGameContext ctx)
    {
        var scene = ctx.Scene;
        var w = ctx.Width;
        var h = ctx.Height;

        // Player 1 right-side-up at bottom end zone.
        scene.Hud.AddText("Player 1", w * 0.5f - 70f, h * 0.92f, 2.4f, Rgba32.White);

        // Player 2 label near top (desktop host; original Unity flipped 180° for opposite seating).
        scene.Hud.AddText("Player 2", w * 0.5f - 70f, h * 0.045f, 2.4f, Rgba32.White);

        // Yard numbers (conceptual 10–50–10).
        DrawYardHud(scene, w, h, "10", 0.18f);
        DrawYardHud(scene, w, h, "20", 0.27f);
        DrawYardHud(scene, w, h, "30", 0.36f);
        DrawYardHud(scene, w, h, "40", 0.45f);
        DrawYardHud(scene, w, h, "50", 0.50f);
        DrawYardHud(scene, w, h, "40", 0.55f);
        DrawYardHud(scene, w, h, "30", 0.64f);
        DrawYardHud(scene, w, h, "20", 0.73f);
        DrawYardHud(scene, w, h, "10", 0.82f);

        if (_phase == Phase.Playing)
        {
            scene.Hud.AddText(
                "Tap your end  ·  P1: bottom / A S  ·  P2: top / W",
                16f,
                h * 0.5f - 10f,
                1.35f,
                new Rgba32(220, 235, 220));
        }
    }

    private static void DrawYardHud(TwoDScene scene, float w, float h, string label, float yFrac)
    {
        var y = h * yFrac;
        scene.Hud.AddText(label, w * 0.08f, y, 1.8f, Rgba32.White);
        scene.Hud.AddText(label, w * 0.88f, y, 1.8f, Rgba32.White);
    }

    private void DrawWinnerBanner(SilkTwoDGameContext ctx)
    {
        if (_pendingWinner is not { } winner)
        {
            return;
        }

        ctx.Scene.Hud.AddText(
            $"Player {winner} wins!",
            ctx.Width * 0.5f - 110f,
            ctx.Height * 0.48f,
            3.2f,
            new Rgba32(40, 40, 40));
    }

    private void PushTitleMenu(SilkTwoDGameContext ctx)
    {
        _phase = Phase.Title;
        ctx.Scene.Menus.Clear();
        ctx.Scene.Menus.Push(new TwoDMenuScreen("TAP DUEL FOOTBALL", [
            new TwoDMenuItem("PLAY", Tag: "play", OnSelect: () =>
            {
                StartCountdown(ctx);
                return "play";
            }),
            new TwoDMenuItem("QUIT", Tag: "quit", OnSelect: () =>
            {
                Environment.Exit(0);
                return "quit";
            }),
        ]));
    }

    private void StartCountdown(SilkTwoDGameContext ctx)
    {
        ctx.Scene.Menus.Clear();
        _match.Reset();
        _pendingWinner = null;
        _countdown = 3f;
        _phase = Phase.Countdown;
        _ = _flows.PushAsync(new CountdownFlowScreen());
    }

    private void PushEndMenu(SilkTwoDGameContext ctx)
    {
        var title = _pendingWinner is { } w ? $"PLAYER {w} WINS" : "GAME OVER";
        ctx.Scene.Menus.Clear();
        ctx.Scene.Menus.Push(new TwoDMenuScreen(title, [
            new TwoDMenuItem("RESET", Tag: "reset", OnSelect: () =>
            {
                StartCountdown(ctx);
                return "reset";
            }),
            new TwoDMenuItem("EXIT", Tag: "exit", OnSelect: () =>
            {
                Environment.Exit(0);
                return "exit";
            }),
        ]));
    }

    private static void FitCamera(SilkTwoDGameContext ctx)
    {
        var scene = ctx.Scene;
        scene.Camera.ViewportWidth = Math.Max(1, ctx.Width);
        scene.Camera.ViewportHeight = Math.Max(1, ctx.Height);

        var worldH = (FieldPainter.FieldHalfLength + FieldPainter.EndZoneDepth) * 2.15f;
        var worldW = FieldPainter.FieldHalfWidth * 2.25f;
        var zoomH = worldH / Math.Max(1, ctx.Height);
        var zoomW = worldW / Math.Max(1, ctx.Width);
        scene.Camera.WorldUnitsPerPixel = MathF.Max(zoomH, zoomW);
    }

    private sealed class TitleFlowScreen : IGameScreen
    {
        public string ScreenId => "title";
        public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class CountdownFlowScreen : IGameScreen
    {
        public string ScreenId => "countdown";
        public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class PlayFlowScreen : IGameScreen
    {
        public string ScreenId => "play";
        public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
