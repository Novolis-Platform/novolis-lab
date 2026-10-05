using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Math.Topology;
using Novolis.Rendering.TwoD;

namespace FrankMoat.Levels;

internal static class WallFactory
{
    public static RaisedWall Box(float minX, float minZ, float maxX, float maxZ, float height, WallMaterial material)
    {
        var footprint = TwoDScenePrimitives.Rectangle(minX, minZ, maxX, maxZ);
        var segments = new WallSegment[]
        {
            new(Xz(minX, minZ), Xz(maxX, minZ), height, material),
            new(Xz(maxX, minZ), Xz(maxX, maxZ), height, material),
            new(Xz(maxX, maxZ), Xz(minX, maxZ), height, material),
            new(Xz(minX, maxZ), Xz(minX, minZ), height, material),
        };
        return new RaisedWall
        {
            Footprint = footprint,
            Height = height,
            Material = material,
            Segments = segments,
        };
    }

    public static RaisedWall Polygon(IReadOnlyList<Vector3> vertices, float height, WallMaterial material)
    {
        var footprint = new Polygon(vertices);
        var segments = new WallSegment[vertices.Count];
        for (var i = 0; i < vertices.Count; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Count];
            segments[i] = new WallSegment(a, b, height, material);
        }

        return new RaisedWall
        {
            Footprint = footprint,
            Height = height,
            Material = material,
            Segments = segments,
        };
    }

    private static Vector3 Xz(float x, float z) => Vector3PlanarExtensions.Xz(x, z);
}
