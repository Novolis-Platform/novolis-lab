using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Rendering;

namespace XFighter.Game;

internal sealed class CockpitHud
{
    private static readonly Color HudGreen = Color.FromArgb(255, 64, 255, 110);
    private static readonly Color HudAmber = Color.FromArgb(255, 255, 200, 70);
    private static readonly Color HudRed = Color.FromArgb(255, 255, 70, 70);
    private static readonly Color PanelDark = Color.FromArgb(240, 10, 12, 18);
    private static readonly Color PanelMid = Color.FromArgb(255, 28, 32, 44);
    private static readonly Color CommBg = Color.FromArgb(210, 8, 14, 10);

    private Texture _cockpitTexture;
    private bool _hasTexture;

    public void Initialize(RayGameContext ctx)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "cockpit_overlay.png");
        if (File.Exists(path))
        {
            var tex = Textures.Load(path);
            if (Textures.IsValid(tex))
            {
                _cockpitTexture = tex;
                _hasTexture = true;
            }
        }
    }

    public void Draw(
        RayGameContext ctx,
        PlayerFlight player,
        int score,
        int enemies,
        float shield,
        HFighter? lockTarget,
        WingmanChatter chatter)
    {
        if (_hasTexture)
        {
            ctx.DrawHudTexture(
                _cockpitTexture,
                new RectangleF(0, 0, ctx.Width, ctx.Height),
                Color.White);
        }
        else
            DrawProceduralFrame(ctx);

        CockpitNoseArt.Draw(ctx, player.Roll);

        var cx = ctx.Width / 2;
        var cy = ctx.Height / 2;
        var reticle = 22;
        ctx.HudLine(cx - reticle, cy, cx - 8, cy, HudGreen);
        ctx.HudLine(cx + 8, cy, cx + reticle, cy, HudGreen);
        ctx.HudLine(cx, cy - reticle, cx, cy - 8, HudGreen);
        ctx.HudLine(cx, cy + 8, cx, cy + reticle, HudGreen);
        ctx.HudRect(cx - 2, cy - 2, 4, 4, HudGreen);

        if (lockTarget is { Active: true })
        {
            ctx.HudText("LOCK", cx - 22, cy + 28, 14, HudAmber);
            ctx.HudText($"RNG {(int)Vector3.Distance(lockTarget.Position, player.Position)}", cx - 36, cy + 44, 12, HudGreen);
        }

        ctx.HudText($"KILLS {score / 100}", 48, ctx.Height - 72, 22, HudGreen);
        ctx.HudText($"H-FIGHTERS {enemies}", 48, ctx.Height - 44, 18, HudAmber);
        ctx.HudText($"THR {(int)(player.Throttle01 * 100)}%", ctx.Width - 200, ctx.Height - 72, 20, HudGreen);
        var shieldColor = shield < 0.3f ? HudRed : HudAmber;
        ctx.HudText($"DEFLECTOR {(int)(shield * 100)}%", ctx.Width - 200, ctx.Height - 44, 18, shieldColor);

        DrawRadar(ctx, player);
        DrawDeflectorBar(ctx, shield);
        DrawCommsPanel(ctx, chatter);
        ctx.HudText("MOUSE: AIM  |  W/S: THROTTLE  |  A/D: ROLL  |  SPACE/LMB: FIRE", 48, 24, 15, Color.FromArgb(255, 140, 160, 175));
        ctx.HudText("RED SQUADRON — X-FIGHTER", 48, 46, 14, HudAmber);
    }

    private static void DrawProceduralFrame(RayGameContext ctx)
    {
        var w = ctx.Width;
        var h = ctx.Height;
        var margin = 38;
        var viewL = (int)(w * 0.12f);
        var viewR = (int)(w * 0.88f);
        var viewT = (int)(h * 0.1f);
        var viewB = (int)(h * 0.9f);

        ctx.HudRect(0, 0, w, margin, PanelDark);
        ctx.HudRect(0, h - margin, w, margin, PanelDark);
        ctx.HudRect(0, 0, viewL, h, PanelDark);
        ctx.HudRect(viewR, 0, w - viewR, h, PanelDark);

        ctx.HudRect(viewL, margin, 8, viewB - margin, PanelMid);
        ctx.HudRect(viewR - 8, margin, 8, viewB - margin, PanelMid);
        ctx.HudRect(viewL, viewT - 8, viewR - viewL, 8, PanelMid);
        ctx.HudRect(viewL, viewB, viewR - viewL, 8, PanelMid);

        for (var i = 0; i < 6; i++)
        {
            var y = margin + 52 + i * 56;
            ctx.HudRect(viewL + 18, y, 100, 36, Color.FromArgb(200, 20, 24, 34));
            ctx.HudRect(viewR - 118, y, 100, 36, Color.FromArgb(200, 20, 24, 34));
            ctx.HudText($"{i + 1}", viewL + 52, y + 10, 16, HudGreen);
        }
    }

    private static void DrawRadar(RayGameContext ctx, PlayerFlight player)
    {
        var rx = ctx.Width - 210;
        var ry = 72;
        var size = 150;
        ctx.HudRect(rx, ry, size, size, Color.FromArgb(200, 6, 12, 8));
        ctx.HudLine(rx, ry + size / 2, rx + size, ry + size / 2, HudGreen);
        ctx.HudLine(rx + size / 2, ry, rx + size / 2, ry + size, HudGreen);
        ctx.HudText("TACTICAL", rx + 32, ry + 8, 14, HudGreen);
        _ = player;
    }

    private static void DrawDeflectorBar(RayGameContext ctx, float shield)
    {
        var x = 48;
        var y = 72;
        var w = 220;
        var h = 18;
        ctx.HudRect(x, y, w, h, Color.FromArgb(255, 24, 28, 36));
        var fill = (int)(w * shield);
        var color = shield < 0.3f ? HudRed : HudAmber;
        ctx.HudRect(x, y, fill, h, color);
        ctx.HudText("DEFLECTOR", x, y - 22, 14, color);
    }

    private static void DrawCommsPanel(RayGameContext ctx, WingmanChatter chatter)
    {
        var line = chatter.CurrentLine;
        if (line is null)
            return;

        var alpha = (int)(chatter.LineAlpha * 255);
        var x = 48;
        var y = 110;
        var w = Math.Min(520, ctx.Width - 96);
        ctx.HudRect(x, y, w, 64, Color.FromArgb(alpha * 200 / 255, CommBg));
        ctx.HudText(chatter.CurrentSpeaker ?? "COMMS", x + 12, y + 8, 13, HudAmber);
        ctx.HudText(line, x + 12, y + 28, 16, Color.FromArgb(alpha, 180, 255, 190));
    }
}
