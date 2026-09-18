using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;

namespace RtsLite.Game;

internal static class RtsMinimap
{
    private const int Size = 150;
    private const int Margin = 16;

    public static void Draw(RayGameContext ctx, RtsArena arena, IReadOnlyList<RtsUnit> units)
    {
        var ox = ctx.Width - Size - Margin;
        var oy = Margin;
        ctx.HudRect(ox - 2, oy - 2, Size + 4, Size + 4, Color.FromArgb(255, 60, 50, 40));
        ctx.HudRect(ox, oy, Size, Size, Color.FromArgb(220, 120, 100, 70));

        var scale = (float)Size / RtsArena.GridSize;
        for (var z = 0u; z < arena.Walls.Height; z++)
        for (var x = 0u; x < arena.Walls.Width; x++)
        {
            Color c;
            if (arena.Walls[x, z, 0] != 0)
                c = x is >= 16 and <= 22 && z is >= 16 and <= 22
                    ? Color.FromArgb(255, 40, 100, 110)
                    : Color.FromArgb(255, 70, 60, 50);
            else if (arena.Tiberium[x, z, 0] != 0)
                c = Color.FromArgb(255, 50, 140, 55);
            else
                c = Color.FromArgb(255, 150, 125, 80);

            var px = (int)(ox + x * scale);
            var py = (int)(oy + z * scale);
            var dot = Math.Max(1, (int)scale);
            ctx.HudRect(px, py, dot, dot, c);
        }

        foreach (var b in arena.Buildings)
        {
            var gx = b.GridX + b.Width * 0.5f;
            var gz = b.GridZ + b.Height * 0.5f;
            var px = (int)(ox + gx * scale);
            var py = (int)(oy + gz * scale);
            var color = b.Team == UnitTeam.Player
                ? Color.FromArgb(255, 80, 140, 255)
                : Color.FromArgb(255, 255, 80, 60);
            ctx.HudRect(px - 2, py - 2, 5, 5, color);
        }

        foreach (var unit in units)
        {
            var gx = unit.Position.X / RtsArena.CellSize;
            var gz = unit.Position.Z / RtsArena.CellSize;
            var px = (int)(ox + gx * scale);
            var py = (int)(oy + gz * scale);
            var color = unit.Team == UnitTeam.Player
                ? Color.FromArgb(255, 120, 220, 255)
                : Color.FromArgb(255, 255, 100, 80);
            ctx.HudRect(px, py, 2, 2, color);
        }

        ctx.HudText("MAP", ox + 6, oy + 4, 12, Color.FromArgb(255, 220, 200, 160));
    }
}
