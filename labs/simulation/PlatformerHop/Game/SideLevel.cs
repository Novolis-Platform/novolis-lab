using Novolis.Math.Arrays;

namespace PlatformerHop.Game;

/// <summary>Side-view tile map: X columns, Y rows (height), Z = 0.</summary>
internal sealed class SideLevel
{
    public const float CellSize = 0.5f;
    public const float PlayerRadius = 0.22f;

    public DenseGrid<byte> Tiles { get; }

    public float WidthWorld => Tiles.Width * CellSize;
    public float HeightWorld => Tiles.Height * CellSize;

    private SideLevel(DenseGrid<byte> tiles) => Tiles = tiles;

    public static SideLevel CreateDemo()
    {
        const uint w = 48;
        const uint h = 24;
        var grid = new DenseGrid<byte>(w, h);

        for (var y = 0u; y < h; y++)
        for (var x = 0u; x < w; x++)
            grid[x, y, 0] = y == 0 ? (byte)1 : (byte)0;

        void Platform(uint x0, uint y0, uint len)
        {
            for (var i = 0u; i < len; i++)
                grid[x0 + i, y0, 0] = 1;
        }

        Platform(6, 4, 5);
        Platform(14, 7, 4);
        Platform(20, 10, 6);
        Platform(30, 8, 5);
        Platform(36, 12, 4);
        Platform(42, 15, 4);

        return new SideLevel(grid);
    }

    public bool IsSolidAtWorld(float x, float y)
    {
        var gx = (int)(x / CellSize);
        var gy = (int)(y / CellSize);
        if (gx < 0 || gy < 0 || gx >= Tiles.Width || gy >= Tiles.Height)
            return gy < 0;
        return Tiles[(uint)gx, (uint)gy, 0] != 0;
    }
}
