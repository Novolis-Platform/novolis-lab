using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;

namespace DoomLite3D.Game;

internal sealed class DoomLiteGame
{
    private static readonly Color Ambient = Color.FromArgb(255, 28, 24, 30);
    private static readonly Color MuzzleFlash = Color.FromArgb(255, 255, 220, 120);

    private readonly DiagnosticsOverlay _diagnostics = new();

    private LevelMap _level = null!;
    private readonly PlayerController _player = new();
    private readonly EnemySystem _enemies = new();
    private readonly WeaponHud _hud = new();
    private readonly PlayerCombatState _combat = new();

    public void Initialize(RayGameContext ctx)
    {
        ctx.DisableCursor();
        _enemies.Initialize(ctx);
        _hud.Initialize(ctx);
        RegenerateLevel();
    }

    public void Update(RayGameContext ctx)
    {
        if (ctx.IsKeyPressed((KeyboardKey)290))
            RegenerateLevel();

        _diagnostics.ToggleIfKeyPressed(ctx);

        if (ctx.IsKeyPressed(KeyboardKey.R))
            _combat.TryStartReload();

        _combat.Tick(ctx.DeltaSeconds);

        var canAct = !_combat.IsDead;
        _player.Update(ctx, _level, canAct);
        _enemies.Update(ctx, _level, _player, _combat);

        ctx.Clear(Ambient);
        var camera = _player.BuildRaylibCamera();
        ctx.BeginWorld(camera);
        LevelRenderer.Draw(ctx, camera, _level);
        _enemies.Draw(ctx, camera);
        _enemies.DrawBolts(ctx);
        DrawMuzzleBolt(ctx, camera);
        ctx.EndWorld();
        _hud.Draw(ctx, _combat, _enemies.CountAlive(), _level, _player, _enemies);
        _diagnostics.Draw(ctx, (_, lines) =>
        {
            lines.Add($"Enemies {_enemies.CountAlive()}/{_enemies.CountTotal()}");
            lines.Add($"Seed {_level.Seed}");
        });
    }

    private void DrawMuzzleBolt(RayGameContext ctx, Novolis.Raylib.Rendering.Camera camera)
    {
        if (_combat.MuzzleFlashTimer <= 0f)
            return;

        var origin = _player.EyePosition;
        var end = origin + _player.Camera.GetLookDirection() * 2.5f;
        ctx.DrawBolt(origin, end, MuzzleFlash);
    }

    private void RegenerateLevel()
    {
        _level = LevelMap.CreateRandom();
        _player.Reset(_level);
        _enemies.Reset(_level);
        _combat.Reset();
    }
}
