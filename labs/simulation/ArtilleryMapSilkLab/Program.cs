using ArtillerySimulator.Game;
using Novolis.Math.Geometry;
using Novolis.Silk;

namespace ArtilleryMapSilkLab;

/// <summary>
/// Meter-aligned Kristiansand map blit (same raster as extract preview). North is up (+Z in world meters).
/// </summary>
internal static class Program
{
    public static void Main()
    {
        Rgba32[]? frame = null;
        var width = 0;
        var height = 0;

        SilkGame.Run("Kristiansand map — Silk meter grid", 1280, 820, initialize: null, update: silk =>
        {
            if (silk.Width != width || silk.Height != height)
            {
                width = silk.Width;
                height = silk.Height;
                frame = KristiansandMapRaster.BuildFrame(width, height, drawLinework: true);
            }

            if (frame is not null)
                silk.Blit(frame, width, height);

            if (silk.IsKeyPressed(Key.R))
                frame = KristiansandMapRaster.BuildFrame(width, height, drawLinework: true);
        });
    }
}
