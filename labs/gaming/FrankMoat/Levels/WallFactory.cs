using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Math.Topology;
using Novolis.Rendering.Planar;

namespace FrankMoat.Levels;

internal static class WallFactory
{
    public static RaisedWall Box(float minX, float minZ, float maxX, float maxZ, float height, WallMaterial material) =>
        FromFootprint(PlanarScenePrimitives.Rectangle(minX, minZ, maxX, maxZ), height, material);

    public static RaisedWall Polygon(IReadOnlyList<Vector3> vertices, float height, WallMaterial material) =>
        FromFootprint(new Polygon(vertices), height, material);

    private static RaisedWall FromFootprint(Polygon footprint, float height, WallMaterial material)
    {
        var prism = footprint.Extrude(PlanarElevation.Offset(height));
        var segments = new WallSegment[prism.Sides.Count];
        for (var i = 0; i < prism.Sides.Count; i++)
        {
            var side = prism.Sides[i];
            segments[i] = new WallSegment(side.Start, side.End, height, material);
        }

        return new RaisedWall
        {
            Footprint = footprint,
            Height = height,
            Material = material,
            Segments = segments,
        };
    }
}
