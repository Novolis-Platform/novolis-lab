using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Math.Topology;
using Novolis.Rendering.Planar;

namespace TopDownDoom.Game.ImportedDoom;

internal static class ImportedDoomLevelBuilder
{
    private const float MapScale = 1f / 32f;
    private const float WallHeightScale = 1f / 96f;
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

                var polygon = new Polygon(loop.Select(index => ToWorld(map, map.Vertices[index].X, map.Vertices[index].Y)));
                var light = Math.Clamp(sector.LightLevel, (short)0, (short)255);
                var tint = (byte)Math.Clamp(28 + light / 4, 28, 96);
                var floor = new PlanarStaticPolygon(polygon, new Rgba32(tint, tint, (byte)Math.Clamp(tint + 8, 0, 255)))
                {
                    DrawFilled = true,
                    SortKey = 0,
                };
                if (catalog.TryLoad(scene.Textures, "flats", sector.FloorFlat, out var texture))
                {
                    floor.Texture = texture;
                    floor.TextureMeters = 2f;
                }

                scene.StaticPolygons.Add(floor);
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

            var height = WallHeight(map, frontSector, backSector);
            var footprint = WallFootprint(start, end, WallThickness);
            var color = solid ? new Rgba32(70, 62, 78) : new Rgba32(52, 48, 62);
            var faceTexture = catalog.TryLoad(scene.Textures, "textures", textureName, out var texture)
                ? texture
                : default;
            var visualStart = scene.StaticPolygons.Count;
            PlanarWallComposer.Add(
                scene,
                footprint.Extrude(PlanarElevation.Offset(height)),
                faceTexture,
                2f,
                false,
                faceTexture,
                color,
                color);
            var visuals = scene.StaticPolygons.Skip(visualStart).ToArray();
            foreach (var visual in visuals)
            {
                visual.SortKey = 200 + lineIndex;
            }
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

    private static float WallHeight(ImportedDoomMap map, int frontSector, int backSector)
    {
        var sector = frontSector >= 0 && frontSector < map.Sectors.Length
            ? map.Sectors[frontSector]
            : default;
        var height = sector.CeilingHeight - sector.FloorHeight;
        if (backSector >= 0 && backSector < map.Sectors.Length)
        {
            var back = map.Sectors[backSector];
            height = Math.Max(height, back.CeilingHeight - back.FloorHeight);
        }

        return Math.Clamp(height * WallHeightScale, 0.25f, 2.8f);
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
