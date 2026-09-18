using System.Drawing;
using Novolis.Raylib.Game;
using Novolis.Raylib.Rendering;

namespace DoomLite3D.Game;

internal sealed class WeaponHud
{
    private Texture _weapon;
    private bool _hasWeapon;

    public void Initialize(RayGameContext ctx)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "weapon.png");
        if (!File.Exists(path))
            return;
        var tex = ctx.LoadTexture(path);
        if (ctx.IsTextureValid(tex))
        {
            _weapon = tex;
            _hasWeapon = true;
        }
    }

    public void Draw(
        RayGameContext ctx,
        PlayerCombatState combat,
        int aliveEnemies,
        LevelMap level,
        PlayerController player,
        EnemySystem enemies)
    {
        DrawHealthBar(ctx, combat);
        MinimapHud.Draw(ctx, level, player, enemies);
        DrawCrosshair(ctx, combat);
        DrawWeapon(ctx, combat);
        DrawAmmo(ctx, combat);
        DrawStatus(ctx, combat, aliveEnemies);
    }

    private static void DrawHealthBar(RayGameContext ctx, PlayerCombatState combat)
    {
        const int x = 16;
        const int y = 16;
        const int w = 200;
        const int h = 18;

        ctx.HudRect(x, y, w, h, Color.FromArgb(200, 40, 30, 30));
        var pct = combat.Health / combat.MaxHealth;
        var fillW = (int)(w * pct);
        var fillColor = pct > 0.5f
            ? Color.FromArgb(255, 60, 180, 80)
            : pct > 0.25f
                ? Color.FromArgb(255, 200, 160, 50)
                : Color.FromArgb(255, 200, 60, 50);
        if (fillW > 0)
            ctx.HudRect(x, y, fillW, h, fillColor);

        ctx.HudText($"HP {(int)combat.Health}", x + 6, y + 2, 16, Color.White);
    }

    private static void DrawCrosshair(RayGameContext ctx, PlayerCombatState combat)
    {
        var cx = ctx.Width / 2;
        var cy = ctx.Height / 2;
        var bright = combat.HitFlashTimer > 0f || combat.MuzzleFlashTimer > 0f;
        var color = bright
            ? Color.FromArgb(255, 255, 220, 120)
            : Color.FromArgb(255, 220, 220, 200);
        var size = bright ? 14 : 12;
        ctx.HudLine(cx - size, cy, cx + size, cy, color);
        ctx.HudLine(cx, cy - size, cx, cy + size, color);
    }

    private void DrawWeapon(RayGameContext ctx, PlayerCombatState combat)
    {
        if (_hasWeapon)
        {
            var dest = new RectangleF(ctx.Width * 0.5f - 90, ctx.Height - 210, 180, 180);
            ctx.DrawHudTexture(_weapon, dest, Color.White);
            return;
        }

        DrawProceduralWeapon(ctx);
    }

    private static void DrawProceduralWeapon(RayGameContext ctx)
    {
        var bx = (int)(ctx.Width * 0.5f);
        var by = ctx.Height - 120;
        var metal = Color.FromArgb(255, 90, 95, 110);
        var wood = Color.FromArgb(255, 100, 65, 35);
        ctx.HudRect(bx - 40, by, 80, 16, metal);
        ctx.HudRect(bx + 20, by - 4, 28, 24, metal);
        ctx.HudRect(bx - 52, by + 2, 16, 12, wood);
    }

    private static void DrawAmmo(RayGameContext ctx, PlayerCombatState combat)
    {
        var text = combat.IsReloading
            ? "RELOADING..."
            : $"AMMO {combat.Ammo} / {combat.MaxAmmo}  (+{combat.ReserveAmmo})";
        var color = combat.Ammo > 0 ? Color.FromArgb(255, 220, 220, 180) : Color.FromArgb(255, 220, 80, 80);
        var x = ctx.Width - 280;
        var y = ctx.Height - 40;
        ctx.HudText(text, x, y, 20, color);
    }

    private static void DrawStatus(RayGameContext ctx, PlayerCombatState combat, int aliveEnemies)
    {
        if (combat.IsDead)
        {
            var msg = "YOU DIED — press F1 to restart";
            var tw = msg.Length * 12;
            ctx.HudText(msg, ctx.Width / 2 - tw / 2, ctx.Height / 2 - 20, 28, Color.FromArgb(255, 220, 60, 60));
            return;
        }

        ctx.HudText($"Enemies: {aliveEnemies}", 16, 40, 18, Color.FromArgb(255, 200, 200, 180));
        ctx.HudText(
            "WASD move | Space jump | Mouse/Ctrl shoot | R reload | F3 diagnostics | F1 new maze | Esc quit",
            16,
            62,
            14,
            Color.FromArgb(255, 140, 140, 130));
    }
}
