using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Rendering.Planar;
using TopologyPolygon = Novolis.Math.Topology.Polygon;

namespace RtsLitePlanar.Game;

internal static class RtsLitePlanarRender
{
    public static void AddSandField(PlanarScene scene, float size, Rgba32 sand) =>
        scene.AddPlatform(0f, 0f, size, size, sand);

    public static PlanarStaticPolygon AddTankMarker(
        PlanarScene scene,
        Vector3 position,
        Rgba32 fill,
        float facingYaw,
        int sortKey)
    {
        const float halfLen = 0.55f;
        const float halfW = 0.32f;
        var c = MathF.Cos(facingYaw);
        var s = MathF.Sin(facingYaw);
        var poly = new TopologyPolygon([
            RotateCorner(position, -halfLen, -halfW, c, s),
            RotateCorner(position, halfLen, -halfW, c, s),
            RotateCorner(position, halfLen, halfW, c, s),
            RotateCorner(position, -halfLen, halfW, c, s),
        ]);
        var marker = new PlanarStaticPolygon(poly, fill)
        {
            DrawFilled = true,
            DrawOutline = true,
            SortKey = sortKey,
        };
        scene.StaticPolygons.Add(marker);
        return marker;
    }

    public static PlanarStaticPolygon? AddOrderSegment(PlanarScene scene, Vector3 from, Vector3 to, Rgba32 color)
    {
        var delta = to - from;
        delta.Y = 0f;
        var len = delta.Length();
        if (len < 0.15f)
        {
            return null;
        }

        var dir = Vector3.Normalize(delta);
        var perp = new Vector3(-dir.Z, 0f, dir.X) * 0.05f;
        var poly = new TopologyPolygon([
            from + perp,
            from - perp,
            to - perp,
            to + perp,
        ]);
        var segment = new PlanarStaticPolygon(poly, color)
        {
            DrawFilled = true,
            SortKey = 500,
        };
        scene.StaticPolygons.Add(segment);
        return segment;
    }

    public static PlanarStaticPolygon AddFootprintGhost(
        PlanarScene scene,
        Vector3 center,
        float width,
        float depth,
        bool valid)
    {
        var color = valid ? new Rgba32(120, 255, 140, 160) : new Rgba32(255, 90, 90, 160);
        var ghost = new PlanarStaticPolygon(
            PlanarScenePrimitives.Rectangle(
                center.X - width * 0.5f,
                center.Z - depth * 0.5f,
                center.X + width * 0.5f,
                center.Z + depth * 0.5f),
            color)
        {
            DrawFilled = true,
            DrawOutline = true,
            SortKey = 1100,
        };
        scene.StaticPolygons.Add(ghost);
        return ghost;
    }

    private static Vector3 RotateCorner(Vector3 center, float localX, float localZ, float cos, float sin) =>
        center + new Vector3(
            localX * cos - localZ * sin,
            0f,
            localX * sin + localZ * cos);
}
