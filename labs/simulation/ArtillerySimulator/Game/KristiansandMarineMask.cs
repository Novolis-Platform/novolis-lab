using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;

namespace ArtillerySimulator.Game;

/// <summary>Pre-built sea tiles aligned to the baked NDH elevation grid (no per-frame height sampling).</summary>
internal static class KristiansandMarineMask
{
    private static readonly Color Shallow = Color.FromArgb(235, 55, 145, 228);
    private static readonly Color Deep = Color.FromArgb(255, 28, 98, 198);

    private static MarineTile[]? _tiles;

    public static void Draw(RayGameContext ctx)
    {
        _tiles ??= BuildTiles();
        foreach (var tile in _tiles)
            ctx.DrawShipBox(tile.Center, tile.Size, tile.Color);
    }

    private static MarineTile[] BuildTiles()
    {
        var count = KristiansandElevation.Count;
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        var cellX = east / (count - 1);
        var cellZ = north / (count - 1);
        var list = new List<MarineTile>();
        for (var j = 0; j < count; j++)
        {
            for (var i = 0; i < count; i++)
            {
                var h = KristiansandElevation.Meters[j * count + i];
                if (h > MapFeaturesCatalog.SeaLevelMeters)
                    continue;

                var cx = i * cellX + cellX * 0.5f;
                var cz = j * cellZ + cellZ * 0.5f;
                var color = h < 0.35f ? Deep : Shallow;
                list.Add(new MarineTile(
                    new Vector3(cx, 0.55f, cz),
                    new Vector3(cellX * 0.98f, 1.4f, cellZ * 0.98f),
                    color));
            }
        }

        return list.ToArray();
    }

    private readonly record struct MarineTile(Vector3 Center, Vector3 Size, Color Color);
}
