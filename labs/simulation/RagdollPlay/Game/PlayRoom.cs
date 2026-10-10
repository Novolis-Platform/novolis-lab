using System.Numerics;
using Novolis.Math.Arrays;
using Novolis.Physics.Collision.Simple;
using Novolis.Simulation.World;
using Novolis.Simulation.World.Builders;

namespace RagdollPlay.Game;

internal sealed class PlayRoom
{
    public const uint GridSize = 14;
    public const float CellSize = 1f;
    public const float WallHeight = 4f;

    public BvhStaticWorld CollisionWorld { get; }
    /// <summary>XZ center of the room at physics floor height (Y = 0).</summary>
    public Vector3 FloorCenter { get; }
    /// <summary>Volume center of the enclosed room (mid-height).</summary>
    public Vector3 RoomCenter { get; }
    public RoomInteriorBounds InteriorBounds { get; }

    private PlayRoom(
        BvhStaticWorld collisionWorld,
        Vector3 floorCenter,
        Vector3 roomCenter,
        RoomInteriorBounds interiorBounds)
    {
        CollisionWorld = collisionWorld;
        FloorCenter = floorCenter;
        RoomCenter = roomCenter;
        InteriorBounds = interiorBounds;
    }

    public static PlayRoom Create()
    {
        var walls = BuildPerimeterGrid();
        var cells = ToCellSpan(walls);
        var collisionWorld = OccupancyEnclosedRoomMeshBuilder.FromWallGrid(
            walls.Width,
            walls.Height,
            cells,
            CellSize,
            WallHeight);
        var xzCenter = GridSize * CellSize * 0.5f;
        var floorCenter = new Vector3(xzCenter, 0f, xzCenter);
        var roomCenter = new Vector3(xzCenter, WallHeight * 0.5f, xzCenter);
        var interior = RoomInteriorBounds.ForOccupancyGrid(
            GridSize,
            GridSize,
            CellSize,
            WallHeight,
            RagdollBody.SphereRadius);
        return new PlayRoom(collisionWorld, floorCenter, roomCenter, interior);
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
