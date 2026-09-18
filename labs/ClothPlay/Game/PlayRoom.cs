using System.Numerics;
using Novolis.Math.Arrays;
using Novolis.Math.Geometry;
using Novolis.Physics.Cloth;
using Novolis.Physics.Collision.Simple;
using Novolis.Simulation.World;
using Novolis.Simulation.World.Builders;

namespace ClothPlay.Game;

internal sealed class PlayRoom
{
    public const uint GridSize = 14;
    public const float CellSize = 1f;
    public const float WallHeight = 4f;

    public BvhStaticWorld CollisionWorld { get; private set; }
    public Vector3 FloorCenter { get; }
    public RoomInteriorBounds InteriorBounds { get; }
    public SwordProp? Sword { get; private set; }

    private readonly DenseGrid<byte> _walls;
    private readonly byte[] _cells;

    private PlayRoom(
        BvhStaticWorld collisionWorld,
        Vector3 floorCenter,
        RoomInteriorBounds interiorBounds,
        DenseGrid<byte> walls,
        byte[] cells)
    {
        CollisionWorld = collisionWorld;
        FloorCenter = floorCenter;
        InteriorBounds = interiorBounds;
        _walls = walls;
        _cells = cells;
    }

    public static PlayRoom Create()
    {
        var walls = BuildPerimeterGrid();
        var cells = ToCellArray(walls);
        var collisionWorld = OccupancyEnclosedRoomMeshBuilder.FromWallGrid(
            walls.Width,
            walls.Height,
            cells,
            CellSize,
            WallHeight);
        var xzCenter = GridSize * CellSize * 0.5f;
        var floorCenter = new Vector3(xzCenter, 0f, xzCenter);
        var interior = RoomInteriorBounds.ForOccupancyGrid(
            GridSize,
            GridSize,
            CellSize,
            WallHeight,
            ClothSheet.ParticleRadius);
        return new PlayRoom(collisionWorld, floorCenter, interior, walls, cells);
    }

    /// <summary>Rebuilds collision with a horizontal katana (edge up/down); returns the contact cutting blade.</summary>
    public ClothBlade InstallKatana(KatanaEdge edge = KatanaEdge.Up)
    {
        Sword = SwordProp.CreateKatana(FloorCenter, edge);
        var verts = new List<Vector3>();
        var tris = new List<int>();

        AppendRoomMesh(verts, tris);
        Sword.AppendCollisionMesh(verts, tris);
        CollisionWorld = new BvhStaticWorld(new TriangleMesh(verts.ToArray(), tris.ToArray()));
        return Sword.ContactBlade;
    }

    public void ClearSword()
    {
        Sword = null;
        CollisionWorld = OccupancyEnclosedRoomMeshBuilder.FromWallGrid(
            _walls.Width,
            _walls.Height,
            _cells,
            CellSize,
            WallHeight);
    }

    private void AppendRoomMesh(List<Vector3> verts, List<int> tris)
    {
        for (var y = 0u; y < GridSize; y++)
        for (var x = 0u; x < GridSize; x++)
        {
            if (_walls[x, y, 0] == 0)
                continue;

            var cx = (x + 0.5f) * CellSize;
            var cz = (y + 0.5f) * CellSize;
            var h = WallHeight * 0.5f;
            var hx = CellSize * 0.5f;
            RoomMeshBuilder.AppendBox(verts, tris, cx, h, cz, hx, h, hx);
        }

        var x0 = 0f;
        var x1 = GridSize * CellSize;
        var z0 = 0f;
        var z1 = GridSize * CellSize;
        RoomMeshBuilder.AppendQuad(
            verts,
            tris,
            new(x0, 0f, z0),
            new(x1, 0f, z0),
            new(x1, 0f, z1),
            new(x0, 0f, z1));
        RoomMeshBuilder.AppendQuad(
            verts,
            tris,
            new(x0, WallHeight, z1),
            new(x1, WallHeight, z1),
            new(x1, WallHeight, z0),
            new(x0, WallHeight, z0));
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

    private static byte[] ToCellArray(DenseGrid<byte> walls)
    {
        var cells = new byte[walls.Width * walls.Height];
        for (var y = 0u; y < walls.Height; y++)
        for (var x = 0u; x < walls.Width; x++)
            cells[y * walls.Width + x] = walls[x, y, 0];
        return cells;
    }
}
