using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Math.Topology;
using Novolis.Rendering.Planar;

namespace TopDownDoom.Game.ImportedDoom;

internal static class ImportedDoomLevelBuilder
{
    private const float MapScale = 1f / 32f;
    private const float WallThickness = 0.06f;

    public static ImportedDoomLevelBuild Build(PlanarScene scene, ImportedDoomPackage package)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(package);
        var catalog = new ImportedDoomTextureCatalog(package);
        scene.StaticPolygons.Clear();
        scene.Collision.Clear();
        scene.Camera.ClearColor = new Rgba32(14, 10, 18);

        RenderFloors(scene, package.Map, catalog);
        var segments = new List<ImportedDoomWallSegment>();
        var doors = new List<ImportedDoomDoor>();
        RenderWalls(scene, package.Map, catalog, segments, doors);

        var things = package.Map.Things
            .Select(thing => new ImportedDoomThingSpawn(thing, ToWorld(package.Map, thing.X, thing.Y)))
            .ToArray();
        var player = things.FirstOrDefault(thing => thing.Thing.Type == 1);
        var playerStart = player == default
            ? ToWorld(package.Map, (short)(package.Map.Bounds.MinX + 32), (short)(package.Map.Bounds.MinY + 32))
            : player.Position;
        var exits = package.Map.Linedefs
            .Where(line => line.Special is 11 or 51 or 52 or 124 or 198)
            .Where(line => line.StartVertex < package.Map.Vertices.Length && line.EndVertex < package.Map.Vertices.Length)
            .Select(line =>
            {
                var start = package.Map.Vertices[line.StartVertex];
                var end = package.Map.Vertices[line.EndVertex];
                return ToWorld(
                    package.Map,
                    (short)((start.X + end.X) / 2),
                    (short)((start.Y + end.Y) / 2));
            })
            .ToArray();
        return new ImportedDoomLevelBuild
        {
            Package = package,
            Textures = catalog,
            WallSegments = segments.ToArray(),
            Things = things,
            PlayerStart = playerStart,
            PlayerAngle = player == default ? (short)0 : player.Thing.Angle,
            ExitPositions = exits,
            Doors = doors.ToArray(),
        };
    }

    public static Vector3 ToWorld(ImportedDoomMap map, short x, short y) =>
        new(
            1f + (x - map.Bounds.MinX) * MapScale,
            0f,
            1f + (y - map.Bounds.MinY) * MapScale);

    private static void RenderFloors(
        PlanarScene scene,
        ImportedDoomMap map,
        ImportedDoomTextureCatalog catalog)
    {
        for (var sectorIndex = 0; sectorIndex < map.Sectors.Length; sectorIndex++)
        {
            var sector = map.Sectors[sectorIndex];
            foreach (var loop in FindSectorLoops(map, sectorIndex))
            {
                if (loop.Count < 3)
                {
                    continue;
                }

                var vertices = loop
                    .Where(index => index < map.Vertices.Length)
                    .Select(index => ToWorld(map, map.Vertices[index].X, map.Vertices[index].Y))
                    .ToArray();
                var triangles = TriangulateLoop(vertices);
                if (triangles.Count == 0)
                {
                    continue;
                }

                var light = Math.Clamp(sector.LightLevel, (short)0, (short)255);
                var tint = (byte)Math.Clamp(28 + light / 4, 28, 96);
                var floorTexture = catalog.TryLoad(scene.Textures, "flats", sector.FloorFlat, out var texture)
                    ? texture
                    : default;
                foreach (var triangle in triangles)
                {
                    var floor = new PlanarStaticPolygon(triangle, new Rgba32(tint, tint, (byte)Math.Clamp(tint + 8, 0, 255)))
                    {
                        DrawFilled = true,
                        Layer = PlanarDrawLayer.Background,
                        SortKey = floorTexture.IsValid ? floorTexture.Value : 0,
                        Texture = floorTexture,
                        TextureMeters = 2f,
                    };
                    scene.StaticPolygons.Add(floor);
                }
            }
        }
    }

    private static void RenderWalls(
        PlanarScene scene,
        ImportedDoomMap map,
        ImportedDoomTextureCatalog catalog,
        ICollection<ImportedDoomWallSegment> segments,
        ICollection<ImportedDoomDoor> doors)
    {
        for (var lineIndex = 0; lineIndex < map.Linedefs.Length; lineIndex++)
        {
            var line = map.Linedefs[lineIndex];
            if (line.StartVertex >= map.Vertices.Length || line.EndVertex >= map.Vertices.Length)
            {
                continue;
            }

            var start = ToWorld(map, map.Vertices[line.StartVertex].X, map.Vertices[line.StartVertex].Y);
            var end = ToWorld(map, map.Vertices[line.EndVertex].X, map.Vertices[line.EndVertex].Y);
            var direction = end - start;
            var length = direction.LengthXz();
            if (length < 0.01f)
            {
                continue;
            }

            var right = GetSidedef(map, line.RightSidedef);
            var left = GetSidedef(map, line.LeftSidedef);
            var frontSector = right?.Sector ?? left?.Sector ?? -1;
            var backSector = left?.Sector ?? -1;
            var isDoor = IsDoorSpecial(line.Special);
            var solid = (line.Flags & 1) != 0 || right is null || left is null || isDoor;
            var textureName = ChooseTexture(right, left);
            if (!solid && string.IsNullOrEmpty(textureName))
            {
                continue;
            }

            var footprint = WallFootprint(start, end, WallThickness);
            var color = solid ? new Rgba32(70, 62, 78) : new Rgba32(52, 48, 62);
            var faceTexture = catalog.TryLoad(scene.Textures, "textures", textureName, out var texture)
                ? texture
                : default;
            var visualStart = scene.StaticPolygons.Count;
            scene.StaticPolygons.Add(new PlanarStaticPolygon(footprint, color)
            {
                Texture = faceTexture,
                TextureMeters = 2f,
                WallPart = PlanarWallPartKind.Side,
                SortKey = 1000 + (faceTexture.IsValid ? faceTexture.Value : 0),
            });
            var visuals = scene.StaticPolygons.Skip(visualStart).ToArray();
            if (solid)
            {
                var collider = new PlanarCollider(footprint);
                scene.Collision.AddStatic(collider);
                var segment = new ImportedDoomWallSegment(
                    start,
                    end,
                    frontSector,
                    backSector,
                    true,
                    textureName,
                    lineIndex);
                segments.Add(segment);
                if (isDoor)
                {
                    doors.Add(new ImportedDoomDoor
                    {
                        SourceLineIndex = lineIndex,
                        Tag = line.Tag,
                        Special = line.Special,
                        Center = (start + end) * 0.5f,
                        Segment = segment,
                        Collider = collider,
                        Visuals = visuals,
                    });
                }
            }
        }
    }

    private static List<Polygon> TriangulateLoop(IReadOnlyList<Vector3> source)
    {
        var points = new List<Vector3>(source.Count);
        foreach (var point in source)
        {
            if (points.Count == 0 || Vector3.DistanceSquared(points[^1], point) > 1e-8f)
            {
                points.Add(point);
            }
        }

        if (points.Count > 1 && Vector3.DistanceSquared(points[0], points[^1]) <= 1e-8f)
        {
            points.RemoveAt(points.Count - 1);
        }

        if (points.Count < 3)
        {
            return [];
        }

        var area = SignedArea(points);
        if (MathF.Abs(area) <= 1e-6f)
        {
            return [];
        }

        var winding = area > 0f ? 1f : -1f;
        var indices = Enumerable.Range(0, points.Count).ToList();
        var triangles = new List<Polygon>(points.Count - 2);
        var guard = points.Count * points.Count;
        while (indices.Count > 3 && guard-- > 0)
        {
            var clipped = false;
            for (var i = 0; i < indices.Count; i++)
            {
                var previous = indices[(i + indices.Count - 1) % indices.Count];
                var current = indices[i];
                var next = indices[(i + 1) % indices.Count];
                var cross = Cross(points[previous], points[current], points[next]);
                if (cross * winding <= 1e-6f)
                {
                    continue;
                }

                var containsVertex = false;
                for (var j = 0; j < indices.Count; j++)
                {
                    var candidate = indices[j];
                    if (candidate == previous || candidate == current || candidate == next)
                    {
                        continue;
                    }

                    if (PointInTriangle(points[candidate], points[previous], points[current], points[next]))
                    {
                        containsVertex = true;
                        break;
                    }
                }

                if (containsVertex)
                {
                    continue;
                }

                triangles.Add(new Polygon([points[previous], points[current], points[next]]));
                indices.RemoveAt(i);
                clipped = true;
                break;
            }

            if (clipped)
            {
                continue;
            }

            var collinear = -1;
            for (var i = 0; i < indices.Count; i++)
            {
                var previous = indices[(i + indices.Count - 1) % indices.Count];
                var current = indices[i];
                var next = indices[(i + 1) % indices.Count];
                if (MathF.Abs(Cross(points[previous], points[current], points[next])) <= 1e-6f)
                {
                    collinear = i;
                    break;
                }
            }

            if (collinear < 0)
            {
                return [];
            }

            indices.RemoveAt(collinear);
        }

        if (indices.Count == 3)
        {
            var a = points[indices[0]];
            var b = points[indices[1]];
            var c = points[indices[2]];
            if (MathF.Abs(Cross(a, b, c)) > 1e-6f)
            {
                triangles.Add(new Polygon([a, b, c]));
            }
        }

        return triangles;
    }

    private static float SignedArea(IReadOnlyList<Vector3> points)
    {
        var area = 0f;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            area += a.X * b.Z - b.X * a.Z;
        }

        return area * 0.5f;
    }

    private static float Cross(Vector3 a, Vector3 b, Vector3 c) =>
        (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);

    private static bool PointInTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        var ab = Cross(a, b, point);
        var bc = Cross(b, c, point);
        var ca = Cross(c, a, point);
        return (ab >= -1e-6f && bc >= -1e-6f && ca >= -1e-6f)
            || (ab <= 1e-6f && bc <= 1e-6f && ca <= 1e-6f);
    }

    private static bool IsDoorSpecial(short special) =>
        special is 1 or 2 or 3 or 4
            or 26 or 27 or 28
            or 31 or 32 or 33 or 34
            or 117 or 118;

    private static List<List<ushort>> FindSectorLoops(ImportedDoomMap map, int sectorIndex)
    {
        var edges = new List<(ushort Start, ushort End)>();
        for (var i = 0; i < map.Linedefs.Length; i++)
        {
            var line = map.Linedefs[i];
            var right = GetSidedef(map, line.RightSidedef);
            var left = GetSidedef(map, line.LeftSidedef);
            if (right?.Sector == sectorIndex)
            {
                edges.Add((line.StartVertex, line.EndVertex));
            }

            if (left?.Sector == sectorIndex)
            {
                edges.Add((line.EndVertex, line.StartVertex));
            }
        }

        var loops = new List<List<ushort>>();
        while (edges.Count > 0)
        {
            var first = edges[0];
            edges.RemoveAt(0);
            var loop = new List<ushort> { first.Start };
            var current = first.End;
            var guard = 0;
            while (current != first.Start && guard++ <= map.Linedefs.Length)
            {
                loop.Add(current);
                var nextIndex = edges.FindIndex(edge => edge.Start == current);
                if (nextIndex < 0)
                {
                    break;
                }

                current = edges[nextIndex].End;
                edges.RemoveAt(nextIndex);
            }

            if (current == first.Start && loop.Count >= 3)
            {
                loops.Add(loop);
            }
        }

        return loops;
    }

    private static ImportedDoomSidedef? GetSidedef(ImportedDoomMap map, short index)
    {
        return index >= 0 && index < map.Sidedefs.Length ? map.Sidedefs[index] : null;
    }

    private static string ChooseTexture(ImportedDoomSidedef? right, ImportedDoomSidedef? left)
    {
        var candidates = new[]
        {
            right?.MiddleTexture,
            right?.UpperTexture,
            right?.LowerTexture,
            left?.MiddleTexture,
            left?.UpperTexture,
            left?.LowerTexture,
        };
        return candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate) && candidate != "-") ?? string.Empty;
    }

    private static Polygon WallFootprint(Vector3 start, Vector3 end, float thickness)
    {
        var direction = end - start;
        direction.Y = 0f;
        direction = Vector3.Normalize(direction);
        var normal = new Vector3(-direction.Z, 0f, direction.X) * thickness;
        return new Polygon([start + normal, end + normal, end - normal, start - normal]);
    }
}
