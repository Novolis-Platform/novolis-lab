using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Simulation.View;

namespace DoomLite3D.Game;

internal static class MinimapHud
{
    private const int MapSize = 160;
    private const int Margin = 16;

    private static readonly Color PanelBg = Color.FromArgb(200, 12, 14, 18);
    private static readonly Color PanelBorder = Color.FromArgb(255, 60, 70, 90);
    private static readonly Color WallColor = Color.FromArgb(255, 55, 58, 68);
    private static readonly Color FloorColor = Color.FromArgb(255, 28, 30, 36);
    private static readonly Color PlayerColor = Color.FromArgb(255, 120, 220, 255);
    private static readonly Color EnemyColor = Color.FromArgb(255, 220, 70, 70);
    private static readonly Color PackRoomColor = Color.FromArgb(255, 200, 140, 50);
    private static readonly Color BossRoomColor = Color.FromArgb(255, 220, 60, 90);

    public static void Draw(
        RayGameContext ctx,
        LevelMap level,
        PlayerController player,
        EnemySystem enemies)
    {
        var originX = ctx.Width - MapSize - Margin;
        var originY = Margin;

        ctx.HudRect(originX - 2, originY - 2, MapSize + 4, MapSize + 4, PanelBorder);
        ctx.HudRect(originX, originY, MapSize, MapSize, PanelBg);

        var w = level.Walls.Width;
        var h = level.Walls.Height;
        var cellPx = (float)MapSize / Math.Max(w, h);
        var centerX = originX + MapSize / 2f;
        var centerY = originY + MapSize / 2f;

        var playerGx = player.Camera.Position.X / LevelMap.CellSize;
        var playerGz = player.Camera.Position.Z / LevelMap.CellSize;
        var forward = player.Camera.GetForwardXZ();
        var right = player.Camera.GetRightXZ();

        foreach (var room in level.Rooms)
        {
            if (room.Kind is RoomKind.Calm)
                continue;

            var rcx = room.X + room.Width * 0.5f;
            var rcz = room.Z + room.Height * 0.5f;
            var (px, py) = WorldToMinimap(rcx, rcz, playerGx, playerGz, centerX, centerY, cellPx, forward, right);
            var dot = room.Kind == RoomKind.Boss ? 5 : 3;
            var color = room.Kind == RoomKind.Boss ? BossRoomColor : PackRoomColor;
            ctx.HudRect(px - dot / 2, py - dot / 2, dot, dot, color);
        }

        for (var z = 0u; z < h; z++)
        for (var x = 0u; x < w; x++)
        {
            var isWall = level.Walls.Get(x, z) != 0;
            var (px, py) = WorldToMinimap(x + 0.5f, z + 0.5f, playerGx, playerGz, centerX, centerY, cellPx, forward, right);
            var dot = Math.Max(1, (int)(cellPx * 0.85f));
            if (px < originX || py < originY || px >= originX + MapSize || py >= originY + MapSize)
                continue;
            ctx.HudRect(px, py, dot, dot, isWall ? WallColor : FloorColor);
        }

        foreach (var enemy in enemies.Enemies)
        {
            if (!enemy.Alive)
                continue;

            var ex = enemy.Position.X / LevelMap.CellSize;
            var ez = enemy.Position.Z / LevelMap.CellSize;
            var (px, py) = WorldToMinimap(ex, ez, playerGx, playerGz, centerX, centerY, cellPx, forward, right);
            px -= 1;
            py -= 1;
            if (px < originX || py < originY || px >= originX + MapSize - 2 || py >= originY + MapSize - 2)
                continue;
            var dot = enemy.Kind == EnemyKind.Boss ? 4 : 3;
            ctx.HudRect(px, py, dot, dot, EnemyColor);
        }

        ctx.HudRect((int)centerX - 2, (int)centerY - 2, 4, 4, PlayerColor);
        ctx.HudText("MAP", originX + 6, originY + 4, 12, Color.FromArgb(255, 140, 150, 170));
    }

    /// <summary>
    /// Heading-up minimap: project world XZ offset onto camera forward/right, then map to HUD pixels.
    /// Screen Y grows downward, so forward (ahead) uses a minus sign — not a library bug.
    /// </summary>
    private static (int Px, int Py) WorldToMinimap(
        float gx,
        float gz,
        float playerGx,
        float playerGz,
        float centerX,
        float centerY,
        float cellPx,
        Vector3 forward,
        Vector3 right)
    {
        var dx = gx - playerGx;
        var dz = gz - playerGz;
        var ahead = dx * forward.X + dz * forward.Z;
        var strafe = dx * right.X + dz * right.Z;
        var px = (int)(centerX + strafe * cellPx);
        var py = (int)(centerY - ahead * cellPx);
        return (px, py);
    }
}
