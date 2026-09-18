using System.Numerics;
using Novolis.Math.Arrays;

namespace DoomLite3D.Game;

internal enum RoomKind
{
    Calm,
    Pack,
    Boss,
}

internal enum EnemyKind
{
    Grunt,
    Boss,
}

internal readonly record struct RoomRect(int X, int Z, int Width, int Height, RoomKind Kind);

internal sealed class LevelMap
{
    public const float CellSize = 1f;
    public const float WallHeight = 3f;

    public DenseGrid<byte> Walls { get; }
    public IReadOnlyList<EnemySpawn> Enemies { get; }
    public IReadOnlyList<RoomRect> Rooms { get; }
    public GridIndex PlayerSpawn { get; }
    public int Seed { get; }

    private LevelMap(
        DenseGrid<byte> walls,
        GridIndex playerSpawn,
        IReadOnlyList<EnemySpawn> enemies,
        IReadOnlyList<RoomRect> rooms,
        int seed)
    {
        Walls = walls;
        PlayerSpawn = playerSpawn;
        Enemies = enemies;
        Rooms = rooms;
        Seed = seed;
    }

    public static LevelMap CreateRandom(int? seed = null)
    {
        var (walls, spawn, enemies, levelSeed, rooms) = MazeGenerator.Generate(seed);
        return new LevelMap(walls, spawn, enemies, rooms, levelSeed);
    }

    public Vector3 CellToWorld(uint x, uint z, float y = 0f) =>
        new((x + 0.5f) * CellSize, y, (z + 0.5f) * CellSize);
}

internal readonly record struct EnemySpawn(GridIndex Cell, int SpriteIndex, EnemyKind Kind = EnemyKind.Grunt);
