using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Simulation.SpaceCombat;

namespace FreightWing.Game;

internal static class FreightWingHud
{
    private static readonly Color HudGreen = Color.FromArgb(255, 64, 255, 110);
    private static readonly Color HudAmber = Color.FromArgb(255, 255, 200, 70);
    private static readonly Color HudRed = Color.FromArgb(255, 255, 70, 70);
    private static readonly Color Panel = Color.FromArgb(210, 8, 14, 18);

    public static void Draw(
        RayGameContext ctx,
        MissionSession session,
        string craftName,
        string objectiveLine,
        string? comms,
        CrewStation crewStation = CrewStation.Dual)
    {
        var player = session.Player;
        var cx = ctx.Width / 2;
        var cy = ctx.Height / 2;

        ctx.HudRect(0, 0, ctx.Width, 56, Panel);
        ctx.HudRect(0, ctx.Height - 90, ctx.Width, 90, Panel);

        const int reticle = 22;
        ctx.HudLine(cx - reticle, cy, cx - 8, cy, HudGreen);
        ctx.HudLine(cx + 8, cy, cx + reticle, cy, HudGreen);
        ctx.HudLine(cx, cy - reticle, cx, cy - 8, HudGreen);
        ctx.HudLine(cx, cy + 8, cx, cy + reticle, HudGreen);
        ctx.HudRect(cx - 2, cy - 2, 4, 4, HudGreen);

        var lockTarget = session.LockTarget;
        if (lockTarget is { Active: true })
        {
            ctx.HudText("CMD LOCK", cx - 40, cy + 28, 14, HudAmber);
            ctx.HudText($"RNG {(int)Vector3.Distance(lockTarget.Position, player.Position)}", cx - 36, cy + 44, 12, HudGreen);
        }

        var crewLabel = crewStation switch
        {
            CrewStation.Pilot => "CREW PILOT (AI GUN)",
            CrewStation.Gunner => "CREW GUNNER (AI PILOT)",
            _ => "CREW DUAL",
        };

        ctx.HudText($"CRAFT {craftName}", 48, 18, 18, HudAmber);
        ctx.HudText($"PHASE {session.Phase}  |  {crewLabel}", 48, 38, 14, HudGreen);
        ctx.HudText(objectiveLine, 280, 18, 16, HudGreen);

        ctx.HudText($"KILLS {session.Kills}", 48, ctx.Height - 72, 20, HudGreen);
        ctx.HudText($"THR {(int)(player.Throttle01 * 100)}%", 48, ctx.Height - 44, 18, HudAmber);
        var shieldColor = player.Shield < 0.3f * player.Profile.MaxShield ? HudRed : HudAmber;
        ctx.HudText($"SHIELD {(int)(player.Shield / player.Profile.MaxShield * 100)}%", ctx.Width - 220, ctx.Height - 72, 18, shieldColor);
        ctx.HudText($"HULL {(int)(player.Hull / player.Profile.MaxHull * 100)}%", ctx.Width - 220, ctx.Height - 44, 18, HudGreen);

        if (session.CanTransfer)
            ctx.HudText("PRESS T — LAUNCH X-WING", cx - 140, ctx.Height - 120, 20, HudAmber);

        if (!string.IsNullOrEmpty(comms))
            ctx.HudText(comms, 48, 70, 16, HudGreen);

        ctx.HudText("MOUSE AIM | W/S THROTTLE | A/D ROLL | FIRE LMB/SPACE | G CREW | T TRANSFER | ESC MENU", 48, ctx.Height - 22, 13,
            Color.FromArgb(255, 140, 160, 175));
    }
}
