using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace RandoriFight.Game;

internal sealed class RandoriFightGame
{
    private static readonly Color Sky = Color.FromArgb(255, 18, 22, 30);
    private static readonly Color Tatami = Color.FromArgb(255, 178, 148, 96);
    private static readonly Color TatamiLine = Color.FromArgb(255, 128, 96, 58);
    private static readonly Color Rope = Color.FromArgb(255, 188, 48, 42);
    private static readonly Color HudText = Color.FromArgb(255, 238, 228, 210);
    private static readonly Color BarBack = Color.FromArgb(200, 36, 40, 48);
    private static readonly Color BarPlayer = Color.FromArgb(255, 120, 180, 255);
    private static readonly Color BarAi = Color.FromArgb(255, 255, 120, 90);

    private readonly DiagnosticsOverlay _diagnostics = new();
    private readonly SideFightCamera _camera = new();
    private readonly FightAi _ai = new();
    private readonly Fighter _player = new() { IsPlayer = true };
    private readonly Fighter _opponent = new() { IsPlayer = false };

    private string _banner = "Chūdan";

    public void Initialize(RayGameContext ctx)
    {
        _ = ctx;
        ResetRound();
    }

    public void Update(RayGameContext ctx)
    {
        UpdateInput(ctx);
        _player.FaceToward(_opponent.PositionX);
        _player.Update(ctx.DeltaSeconds, ReadMoveAxis(ctx), TechniqueInput.ReadParryHeld(ctx));
        _ai.Update(_opponent, _player, ctx.DeltaSeconds);
        ResolveHits();
        UpdateBanner();

        if (ctx.IsKeyPressed(KeyboardKey.R))
            ResetRound();

        if (ctx.IsKeyPressed(KeyboardKey.Tab))
            FighterRenderer.ToggleSkeletonOverlay();

        _diagnostics.ToggleIfKeyPressed(ctx);

        ctx.Clear(Sky);
        var camera = _camera.Build(ctx.Width, ctx.Height, _player, _opponent);
        ctx.BeginWorld(camera);
        DrawArena(ctx);
        FighterRenderer.Draw(ctx, _player, isPlayer: true);
        FighterRenderer.Draw(ctx, _opponent, isPlayer: false);
        ctx.EndWorld();

        DrawHud(ctx);
        ctx.Text($"A/D ma-ai  |  {TechniqueInput.ControlsHint}  |  Tab rig  R reset", 12, ctx.Height - 36, 16, HudText);
        _diagnostics.Draw(ctx, (_, lines) =>
        {
            lines.Add($"p {_player.Health:F0}  ai {_opponent.Health:F0}");
            lines.Add($"you {TechniqueLabel(_player)}  ai {TechniqueLabel(_opponent)}");
        });
    }

    private void UpdateInput(RayGameContext ctx)
    {
        if (!_player.IsAlive || _player.IsInHitStun)
            return;

        if (!TechniqueInput.ReadParryHeld(ctx) && !_player.IsAttacking)
            _player.SetWalkInput(ReadMoveAxis(ctx));

        var technique = TechniqueInput.ReadAttackPressed(ctx);
        if (technique is { } t)
            _player.TryTechnique(t);
    }

    private static float ReadMoveAxis(RayGameContext ctx)
    {
        var move = 0f;
        if (ctx.IsKeyDown(KeyboardKey.A))
            move -= 1f;
        if (ctx.IsKeyDown(KeyboardKey.D))
            move += 1f;
        return move;
    }

    private void ResolveHits()
    {
        if (_player.IsAlive && _opponent.IsAlive)
        {
            _player.TryApplyAttackHit(_opponent);
            _opponent.TryApplyAttackHit(_player);
        }
    }

    private void UpdateBanner()
    {
        if (!_player.IsAlive && _opponent.Health <= 0f)
        {
            _banner = "Aiuchi";
            return;
        }

        if (!_opponent.IsAlive)
        {
            _banner = "Ippon";
            return;
        }

        if (!_player.IsAlive)
        {
            _banner = "Opponent ippon";
            return;
        }

        _banner = "Katana randori";
    }

    private void ResetRound()
    {
        _player.Reset(-4.2f, facing: 1);
        _opponent.Reset(4.2f, facing: -1);
        _camera.Snap(0f);
        _ai.Reset();
        _banner = "Chūdan";
    }

    private void DrawArena(RayGameContext ctx)
    {
        ctx.DrawPlane(new Vector3(0f, FightArena.FloorY, 0f), new Vector2(FightArena.StageWidth, FightArena.StageDepth), Tatami);

        var halfW = FightArena.StageWidth * 0.5f;
        var halfD = FightArena.StageDepth * 0.5f;
        for (var i = -3; i <= 3; i++)
        {
            var x = i * 1.4f;
            ctx.DrawBolt(new Vector3(x, 0.02f, -halfD), new Vector3(x, 0.02f, halfD), TatamiLine);
        }

        ctx.DrawBolt(new Vector3(-halfW, 0.12f, -halfD), new Vector3(halfW, 0.12f, -halfD), Rope);
        ctx.DrawBolt(new Vector3(-halfW, 0.12f, halfD), new Vector3(halfW, 0.12f, halfD), Rope);
        ctx.DrawBolt(new Vector3(-halfW, 0.12f, -halfD), new Vector3(-halfW, 0.12f, halfD), Rope);
        ctx.DrawBolt(new Vector3(halfW, 0.12f, -halfD), new Vector3(halfW, 0.12f, halfD), Rope);

        ctx.DrawBolt(new Vector3(0f, 0.03f, -halfD), new Vector3(0f, 0.03f, halfD), Color.FromArgb(140, 90, 70, 50));
    }

    private void DrawHud(RayGameContext ctx)
    {
        DrawHealthBar(ctx, 24, 24, 320, 18, _player.Health, BarPlayer);
        DrawHealthBar(ctx, ctx.Width - 344, 24, 320, 18, _opponent.Health, BarAi);
        ctx.Text(_banner, ctx.Width / 2 - 70, 22, 28, HudText);
    }

    private static void DrawHealthBar(RayGameContext ctx, int x, int y, int w, int h, float health, Color fill)
    {
        ctx.Rect(x, y, w, h, BarBack);
        var fillW = (int)(w * Math.Clamp(health / 100f, 0f, 1f));
        if (fillW > 0)
            ctx.Rect(x, y, fillW, h, fill);
    }

    private static string TechniqueLabel(Fighter f) => f.State switch
    {
        FighterState.Idle => "chudan",
        FighterState.Walk => "ayumi",
        FighterState.Men => "men",
        FighterState.Kesa => "kesa",
        FighterState.Thrust => "tsuki",
        FighterState.Do => "do",
        FighterState.Kote => "kote",
        FighterState.Kirioroshi => "kirioroshi",
        FighterState.Parry => "uke",
        FighterState.HitStun => "ukemi",
        FighterState.Ko => "fall",
        _ => f.State.ToString(),
    };
}
