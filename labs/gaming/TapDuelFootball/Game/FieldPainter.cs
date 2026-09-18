using Novolis.Math.Geometry;
using Novolis.Rendering.TwoD;

namespace TapDuelFootball.Game;

/// <summary>Procedural American-football field (turf, yard lines, hash marks).</summary>
internal static class FieldPainter
{
    public static readonly Rgba32 Turf = new(46, 140, 58);
    public static readonly Rgba32 Sideline = new(235, 240, 235);
    public static readonly Rgba32 EndZoneTint = new(38, 118, 48);

    public const float FieldHalfWidth = 5.2f;
    public const float FieldHalfLength = 8f;
    public const float EndZoneDepth = 1.35f;

    public static void Paint(TwoDScene scene)
    {
        var minX = -FieldHalfWidth;
        var maxX = FieldHalfWidth;
        var minZ = -FieldHalfLength - EndZoneDepth;
        var maxZ = FieldHalfLength + EndZoneDepth;

        AddRect(scene, minX, minZ, maxX, maxZ, Turf, filled: true, outline: false, sort: 0);
        AddRect(scene, minX, -FieldHalfLength - EndZoneDepth, maxX, -FieldHalfLength, EndZoneTint, true, false, 1);
        AddRect(scene, minX, FieldHalfLength, maxX, FieldHalfLength + EndZoneDepth, EndZoneTint, true, false, 1);

        // Outer border
        AddLine(scene, minX, minZ, maxX, minZ, 0.08f, 10);
        AddLine(scene, minX, maxZ, maxX, maxZ, 0.08f, 10);
        AddLine(scene, minX, minZ, minX, maxZ, 0.08f, 10);
        AddLine(scene, maxX, minZ, maxX, maxZ, 0.08f, 10);

        // Goal lines
        AddLine(scene, minX, -FieldHalfLength, maxX, -FieldHalfLength, 0.07f, 11);
        AddLine(scene, minX, FieldHalfLength, maxX, FieldHalfLength, 0.07f, 11);

        // Yard lines every 10 yards (field spans 100 yards conceptually)
        for (var i = 1; i <= 9; i++)
        {
            var t = i / 10f;
            var z = -FieldHalfLength + t * (FieldHalfLength * 2f);
            AddLine(scene, minX + 0.12f, z, maxX - 0.12f, z, 0.045f, 12);
        }

        // Hash marks
        var hashXs = new[] { -FieldHalfWidth + 0.55f, -1.1f, 1.1f, FieldHalfWidth - 0.55f };
        for (var i = 0; i <= 20; i++)
        {
            var z = -FieldHalfLength + i * (FieldHalfLength * 2f / 20f);
            foreach (var hx in hashXs)
            {
                AddLine(scene, hx - 0.12f, z, hx + 0.12f, z, 0.035f, 13);
            }
        }
    }

    private static void AddRect(
        TwoDScene scene,
        float minX,
        float minZ,
        float maxX,
        float maxZ,
        Rgba32 color,
        bool filled,
        bool outline,
        int sort)
    {
        scene.StaticPolygons.Add(new TwoDStaticPolygon(
            TwoDScenePrimitives.Rectangle(minX, minZ, maxX, maxZ),
            color)
        {
            DrawFilled = filled,
            DrawOutline = outline,
            OutlineColor = Sideline,
            SortKey = sort,
        });
    }

    private static void AddLine(TwoDScene scene, float x0, float z0, float x1, float z1, float thickness, int sort)
    {
        var dx = x1 - x0;
        var dz = z1 - z0;
        var len = MathF.Sqrt(dx * dx + dz * dz);
        if (len < 1e-4f)
        {
            return;
        }

        var nx = -dz / len * thickness * 0.5f;
        var nz = dx / len * thickness * 0.5f;
        var poly = new Novolis.Math.Topology.Polygon(
        [
            Vector3PlanarExtensions.Xz(x0 + nx, z0 + nz),
            Vector3PlanarExtensions.Xz(x1 + nx, z1 + nz),
            Vector3PlanarExtensions.Xz(x1 - nx, z1 - nz),
            Vector3PlanarExtensions.Xz(x0 - nx, z0 - nz),
        ]);
        scene.StaticPolygons.Add(new TwoDStaticPolygon(poly, Sideline)
        {
            DrawFilled = true,
            DrawOutline = false,
            SortKey = sort,
        });
    }
}
