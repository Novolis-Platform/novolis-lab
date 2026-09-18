using System.Numerics;
using Novolis.Math.Arrays;
using Novolis.Math.Geometry;
using Novolis.Rendering.TwoD;

namespace Novolis.Lab.TwoD;

/// <summary>Builds <see cref="TwoDScene"/> platforms from a planar occupancy grid.</summary>
public static class DenseGridPlatforms
{
    /// <summary>Fills static platforms and colliders for each blocked cell.</summary>
    /// <param name="scene">Target scene.</param>
    /// <param name="grid">Occupancy grid (non-zero = solid).</param>
    /// <param name="cellSize">World size per cell.</param>
    /// <param name="fillColor">Platform fill color.</param>
    public static void AddSolidCells(
        TwoDScene scene,
        DenseGrid<byte> grid,
        float cellSize,
        Rgba32 fillColor)
    {
        for (var z = 0u; z < grid.Height; z++)
        for (var x = 0u; x < grid.Width; x++)
        {
            if (grid[x, z, 0] == 0)
            {
                continue;
            }

            var minX = x * cellSize;
            var minZ = z * cellSize;
            scene.AddPlatform(minX, minZ, minX + cellSize, minZ + cellSize, fillColor);
        }
    }

    /// <summary>Adds a filled square marker for a circle actor.</summary>
    public static TwoDStaticPolygon AddSquareMarker(
        TwoDScene scene,
        Vector3 position,
        float radius,
        Rgba32 fillColor,
        int sortKey = 1000)
    {
        var marker = new TwoDStaticPolygon(
            TwoDScenePrimitives.Rectangle(
                position.X - radius,
                position.Z - radius,
                position.X + radius,
                position.Z + radius),
            fillColor)
        {
            DrawFilled = true,
            DrawOutline = true,
            SortKey = sortKey,
        };
        scene.StaticPolygons.Add(marker);
        return marker;
    }

    /// <summary>Removes and replaces a square marker (per-frame actors).</summary>
    public static TwoDStaticPolygon ReplaceSquareMarker(
        TwoDScene scene,
        TwoDStaticPolygon? previous,
        Vector3 position,
        float radius,
        Rgba32 fillColor,
        int sortKey = 1000)
    {
        if (previous is not null)
        {
            scene.StaticPolygons.Remove(previous);
        }

        return AddSquareMarker(scene, position, radius, fillColor, sortKey);
    }
}
