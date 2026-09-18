using System.Numerics;
using Novolis.Math.Arrays;
using Novolis.Physics.Collision.Simple;
using Novolis.Simulation.World;
using Novolis.Simulation.World.Builders;

namespace BouncingBall.Game;

/// <summary>Occupancy grid room with simulation-built collision meshes.</summary>
internal sealed class RoomWorld
{
    public const uint GridSize = 12;
    public const float CellSize = 1f;
    public const float WallHeight = 4f;

    public DenseGrid<byte> Walls { get; }

    /// <summary>Wall columns only (no floor/ceiling).</summary>
    public BvhStaticWorld SimulationWallColumns { get; }

    /// <summary>Enclosed collision volume (walls + floor + ceiling).</summary>
    public BvhStaticWorld CollisionWorld { get; }

    public Vector3 RoomCenter { get; }

    public RoomInteriorBounds InteriorBounds { get; }

    private RoomWorld(
        DenseGrid<byte> walls,
        BvhStaticWorld simulationWalls,
        BvhStaticWorld collisionWorld,
        Vector3 roomCenter,
        RoomInteriorBounds interiorBounds)
    {
        Walls = walls;
        SimulationWallColumns = simulationWalls;
        CollisionWorld = collisionWorld;
        RoomCenter = roomCenter;
        InteriorBounds = interiorBounds;
    }

    public static RoomWorld Create()
    {
        var walls = BuildPerimeterGrid();
        var cells = ToCellSpan(walls);
        var simulationWalls = OccupancyColumnMeshBuilder.FromWallGrid(
            walls.Width,
            walls.Height,
            cells,
            CellSize,
            WallHeight);
        var collisionWorld = OccupancyEnclosedRoomMeshBuilder.FromWallGrid(
            walls.Width,
            walls.Height,
            cells,
            CellSize,
            WallHeight);
        var roomCenter = new Vector3(GridSize * CellSize * 0.5f, WallHeight * 0.5f, GridSize * CellSize * 0.5f);
        var interior = RoomInteriorBounds.ForOccupancyGrid(
            GridSize,
            GridSize,
            CellSize,
            WallHeight,
            SphereRadius.Value);
        return new RoomWorld(walls, simulationWalls, collisionWorld, roomCenter, interior);
    }

    private static DenseGrid<byte> BuildPerimeterGrid()
    {
        var grid = new DenseGrid<byte>(GridSize, GridSize);
        for (var y = 0u; y < GridSize; y++)
        for (var x = 0u; x < GridSize; x++)
        {
            var border = x == 0 || y == 0 || x == GridSize - 1 || y == GridSize - 1;
            grid[x, y, 0] = border ? (byte)1 : (byte)0;
        }

        return grid;
    }

    private static ReadOnlySpan<byte> ToCellSpan(DenseGrid<byte> walls)
    {
        var cells = new byte[walls.Width * walls.Height];
        for (var y = 0u; y < walls.Height; y++)
        for (var x = 0u; x < walls.Width; x++)
            cells[y * walls.Width + x] = walls[x, y, 0];
        return cells;
    }
}
