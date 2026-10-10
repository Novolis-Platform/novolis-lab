using Novolis.Math.Arrays;

namespace DoomLite3D.Game;

internal static class MazeGenerator
{
    private const int Size = 55;
    private const int CorridorHalfWidth = 1;
    private const int CalmRoomSize = 7;
    private const int CalmRoomOriginX = 1;
    private const int CalmRoomOriginZ = 1;
    private const int CalmExclusionRadius = 10;
    private const int PackRoomSize = 7;
    private const int BossRoomSize = 9;
    private const int PackRoomCount = 6;
    private const int RoomMinSeparation = 10;
    private const int SpawnRoomMinDist = 14;
    private const int MaxDiameter = 70;
    private const int MaxAttempts = 12;
    private const int MinEnemySpacing = 2;
    private const int MaxCorridorEnemies = 38;
    private const int ScatterPercent = 45;

    public static (DenseGrid<byte> Walls, GridIndex PlayerSpawn, List<EnemySpawn> Enemies, int Seed, List<RoomRect> Rooms) Generate(
        int? seed = null)
    {
        var baseSeed = seed ?? Environment.TickCount;
        MazeAttempt? last = null;

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var attemptSeed = HashSeed(baseSeed, attempt);
            var rng = new Random(attemptSeed);
            var maze = TryGenerate(rng);
            if (maze is null)
                continue;

            if (maze.Value.Diameter <= MaxDiameter)
                return BuildLevel(maze.Value, attemptSeed);

            last = maze;
        }

        var fallbackSeed = HashSeed(baseSeed, 0);
        return BuildLevel(
            last ?? TryGenerate(new Random(fallbackSeed)) ?? throw new InvalidOperationException("Level generation failed."),
            fallbackSeed);
    }

    private static MazeAttempt? TryGenerate(Random rng)
    {
        var walls = new byte[Size, Size];
        for (var z = 0; z < Size; z++)
        for (var x = 0; x < Size; x++)
            walls[x, z] = 1;

        var spawnX = CalmRoomOriginX + CalmRoomSize / 2;
        var spawnZ = CalmRoomOriginZ + CalmRoomSize / 2;

        var rooms = new List<RoomSite>();
        CarveRect(walls, CalmRoomOriginX, CalmRoomOriginZ, CalmRoomSize, CalmRoomSize);
        rooms.Add(new RoomSite(
            CalmRoomOriginX,
            CalmRoomOriginZ,
            CalmRoomSize,
            CalmRoomSize,
            RoomKind.Calm,
            spawnX,
            spawnZ));

        var exitZ = CalmRoomOriginZ + CalmRoomSize / 2;
        CarveFloor(walls, CalmRoomOriginX + CalmRoomSize, exitZ);
        CarveFloor(walls, CalmRoomOriginX + CalmRoomSize, exitZ + 1);

        if (!TryPlaceRooms(rng, spawnX, spawnZ, rooms))
            return null;

        foreach (var room in rooms)
        {
            if (room.Kind == RoomKind.Calm)
                continue;
            CarveRect(walls, room.OriginX, room.OriginZ, room.Width, room.Height);
        }

        ConnectRoomsMst(walls, spawnX, spawnZ, rooms);

        var diameter = ComputeMaxDistance(walls, spawnX, spawnZ);
        var roomRects = rooms
            .Select(r => new RoomRect(r.OriginX, r.OriginZ, r.Width, r.Height, r.Kind))
            .ToList();
        return new MazeAttempt(walls, spawnX, spawnZ, diameter, roomRects);
    }

    private static bool TryPlaceRooms(Random rng, int spawnX, int spawnZ, List<RoomSite> rooms)
    {
        var targetRooms = PackRoomCount + 2;
        var attempts = 0;

        while (rooms.Count < targetRooms && attempts < 800)
        {
            attempts++;
            var isBossSlot = rooms.Count == targetRooms - 1;
            var size = isBossSlot ? BossRoomSize : PackRoomSize;
            var kind = isBossSlot ? RoomKind.Boss : RoomKind.Pack;
            var cx = rng.Next(12, Size - 12);
            var cz = rng.Next(12, Size - 12);

            if (Manhattan(cx, cz, spawnX, spawnZ) < SpawnRoomMinDist)
                continue;

            var ox = cx - size / 2;
            var oz = cz - size / 2;
            if (ox < 2 || oz < 2 || ox + size >= Size - 2 || oz + size >= Size - 2)
                continue;

            var overlaps = false;
            foreach (var existing in rooms)
            {
                if (existing.Kind == RoomKind.Calm)
                    continue;
                if (Manhattan(cx, cz, existing.CenterX, existing.CenterZ) < RoomMinSeparation)
                {
                    overlaps = true;
                    break;
                }

                if (RectsOverlap(ox, oz, size, size, existing.OriginX, existing.OriginZ, existing.Width, existing.Height))
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps)
                continue;

            rooms.Add(new RoomSite(ox, oz, size, size, kind, cx, cz));
        }

        return rooms.Count >= targetRooms;
    }

    private static void ConnectRoomsMst(byte[,] walls, int spawnX, int spawnZ, List<RoomSite> rooms)
    {
        var nodes = new List<(int X, int Z)>(rooms.Count) { (spawnX, spawnZ) };
        for (var i = 1; i < rooms.Count; i++)
            nodes.Add((rooms[i].CenterX, rooms[i].CenterZ));

        var edges = new List<(int A, int B, int Weight)>();
        for (var a = 0; a < nodes.Count; a++)
        for (var b = a + 1; b < nodes.Count; b++)
            edges.Add((a, b, Manhattan(nodes[a].X, nodes[a].Z, nodes[b].X, nodes[b].Z)));

        edges.Sort((x, y) => x.Weight.CompareTo(y.Weight));

        var parent = Enumerable.Range(0, nodes.Count).ToArray();
        int Find(int x)
        {
            if (parent[x] != x)
                parent[x] = Find(parent[x]);
            return parent[x];
        }

        void Union(int a, int b)
        {
            var ra = Find(a);
            var rb = Find(b);
            if (ra != rb)
                parent[rb] = ra;
        }

        foreach (var (a, b, _) in edges)
        {
            if (Find(a) == Find(b))
                continue;

            Union(a, b);
            CarveCorridor(walls, nodes[a].X, nodes[a].Z, nodes[b].X, nodes[b].Z);
        }
    }

    private static void CarveCorridor(byte[,] walls, int x1, int z1, int x2, int z2)
    {
        if (System.Math.Abs(x2 - x1) <= System.Math.Abs(z2 - z1))
        {
            CarveThickHLine(walls, x1, x2, z1);
            CarveThickVLine(walls, z1, z2, x2);
        }
        else
        {
            CarveThickVLine(walls, z1, z2, x1);
            CarveThickHLine(walls, x1, x2, z2);
        }
    }

    private static void CarveThickHLine(byte[,] walls, int x0, int x1, int z)
    {
        var from = System.Math.Min(x0, x1);
        var to = System.Math.Max(x0, x1);
        for (var x = from; x <= to; x++)
        for (var dz = -CorridorHalfWidth; dz <= CorridorHalfWidth; dz++)
            CarveFloor(walls, x, z + dz);
    }

    private static void CarveThickVLine(byte[,] walls, int z0, int z1, int x)
    {
        var from = System.Math.Min(z0, z1);
        var to = System.Math.Max(z0, z1);
        for (var z = from; z <= to; z++)
        for (var dx = -CorridorHalfWidth; dx <= CorridorHalfWidth; dx++)
            CarveFloor(walls, x + dx, z);
    }

    private static void CarveRect(byte[,] walls, int ox, int oz, int w, int h)
    {
        for (var dz = 0; dz < h; dz++)
        for (var dx = 0; dx < w; dx++)
            CarveFloor(walls, ox + dx, oz + dz);
    }

    private static void CarveFloor(byte[,] walls, int x, int z)
    {
        if (x <= 0 || x >= Size - 1 || z <= 0 || z >= Size - 1)
            return;
        walls[x, z] = 0;
    }

    private static int ComputeMaxDistance(byte[,] walls, int spawnX, int spawnZ)
    {
        var dist = new int[Size, Size];
        for (var z = 0; z < Size; z++)
        for (var x = 0; x < Size; x++)
            dist[x, z] = -1;

        var queue = new Queue<(int X, int Z)>();
        queue.Enqueue((spawnX, spawnZ));
        dist[spawnX, spawnZ] = 0;
        var max = 0;

        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            var d = dist[x, z];
            max = System.Math.Max(max, d);

            foreach (var (nx, nz) in new[] { (x - 1, z), (x + 1, z), (x, z - 1), (x, z + 1) })
            {
                if (nx < 0 || nx >= Size || nz < 0 || nz >= Size)
                    continue;
                if (walls[nx, nz] != 0 || dist[nx, nz] >= 0)
                    continue;
                dist[nx, nz] = d + 1;
                queue.Enqueue((nx, nz));
            }
        }

        return max;
    }

    private static (DenseGrid<byte> Walls, GridIndex PlayerSpawn, List<EnemySpawn> Enemies, int Seed, List<RoomRect> Rooms) BuildLevel(
        MazeAttempt maze,
        int seed)
    {
        var grid = new DenseGrid<byte>((uint)Size, (uint)Size);
        for (var z = 0; z < Size; z++)
        for (var x = 0; x < Size; x++)
            grid.Set((uint)x, (uint)z, maze.Walls[x, z]);

        var spawn = new GridIndex((uint)maze.SpawnX, (uint)maze.SpawnZ);
        var rng = new Random(seed);
        var enemies = PlaceEnemies(maze.Walls, spawn, maze.Rooms, rng);
        return (grid, spawn, enemies, seed, maze.Rooms);
    }

    private static List<EnemySpawn> PlaceEnemies(
        byte[,] walls,
        GridIndex spawn,
        List<RoomRect> rooms,
        Random rng)
    {
        var placed = new List<EnemySpawn>();
        var usedCells = new HashSet<(int X, int Z)>();

        foreach (var room in rooms)
        {
            if (room.Kind == RoomKind.Calm)
                continue;

            if (room.Kind == RoomKind.Boss)
            {
                var bx = room.X + room.Width / 2;
                var bz = room.Z + room.Height / 2;
                if (TryAddSpawn(placed, usedCells, bx, bz, spriteIndex: 3, EnemyKind.Boss))
                    continue;
            }

            if (room.Kind != RoomKind.Pack)
                continue;

            var interior = CollectInteriorCells(walls, room);
            Shuffle(interior, rng);
            var packCount = rng.Next(4, 7);
            var added = 0;
            foreach (var (cx, cz) in interior)
            {
                if (added >= packCount)
                    break;
                var sprite = 2;
                if (rng.Next(3) == 0)
                    sprite = rng.Next(2);
                if (TryAddSpawn(placed, usedCells, cx, cz, sprite, EnemyKind.Grunt))
                    added++;
            }
        }

        var eligible = new List<GridIndex>();
        for (var z = 0; z < Size; z++)
        for (var x = 0; x < Size; x++)
        {
            if (walls[x, z] != 0)
                continue;
            if (usedCells.Contains((x, z)))
                continue;
            var dist = Manhattan(x, z, (int)spawn.X, (int)spawn.Y);
            if (dist < CalmExclusionRadius)
                continue;
            eligible.Add(new GridIndex((uint)x, (uint)z));
        }

        Shuffle(eligible, rng);
        var corridorTarget = System.Math.Min(
            MaxCorridorEnemies,
            System.Math.Max(0, eligible.Count * ScatterPercent / 100));
        var corridorPlaced = 0;

        foreach (var cell in eligible)
        {
            if (corridorPlaced >= corridorTarget)
                break;

            var cx = (int)cell.X;
            var cz = (int)cell.Y;
            if (TryAddSpawn(placed, usedCells, cx, cz, corridorPlaced % 2, EnemyKind.Grunt))
                corridorPlaced++;
        }

        return placed;
    }

    private static List<(int X, int Z)> CollectInteriorCells(byte[,] walls, RoomRect room)
    {
        var cells = new List<(int X, int Z)>();
        for (var z = room.Z + 1; z < room.Z + room.Height - 1; z++)
        for (var x = room.X + 1; x < room.X + room.Width - 1; x++)
        {
            if (x <= 0 || x >= Size - 1 || z <= 0 || z >= Size - 1)
                continue;
            if (walls[x, z] == 0)
                cells.Add((x, z));
        }

        return cells;
    }

    private static bool TryAddSpawn(
        List<EnemySpawn> placed,
        HashSet<(int X, int Z)> usedCells,
        int x,
        int z,
        int spriteIndex,
        EnemyKind kind)
    {
        if (usedCells.Any(p => Manhattan(p.X, p.Z, x, z) < MinEnemySpacing))
            return false;

        usedCells.Add((x, z));
        placed.Add(new EnemySpawn(new GridIndex((uint)x, (uint)z), spriteIndex, kind));
        return true;
    }

    private static bool RectsOverlap(int ax, int az, int aw, int ah, int bx, int bz, int bw, int bh) =>
        ax < bx + bw && ax + aw > bx && az < bz + bh && az + ah > bz;

    private static int Manhattan(int x1, int z1, int x2, int z2) =>
        System.Math.Abs(x1 - x2) + System.Math.Abs(z1 - z2);

    private static void Shuffle<T>(List<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static int HashSeed(int baseSeed, int attempt) =>
        unchecked(baseSeed * 31 + attempt);

    private readonly record struct RoomSite(
        int OriginX,
        int OriginZ,
        int Width,
        int Height,
        RoomKind Kind,
        int CenterX,
        int CenterZ);

    private readonly record struct MazeAttempt(
        byte[,] Walls,
        int SpawnX,
        int SpawnZ,
        int Diameter,
        List<RoomRect> Rooms);
}
