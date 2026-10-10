using Novolis.Math.Geometry;
using Novolis.Rendering.Planar;

namespace TopDownDoom.Game;

/// <summary>
/// Draws walls with a slight oblique extrusion so doors and corners read at ~70° tilt without a 3D camera.
/// </summary>
internal static class ObliqueWallDrawer
{
    private const float ExtrusionZ = 0.55f;
    private const float ExtrusionX = 0.18f;

    public static (PlanarStaticPolygon Floor, PlanarStaticPolygon North, PlanarCollider Blocker) AddWall(
        PlanarScene scene,
        float minX,
        float minZ,
        float maxX,
        float maxZ,
        Rgba32? floorTint = null)
    {
        var floor = new Rgba32(48, 42, 58);
        var face = new Rgba32(72, 64, 82);
        var lip = new Rgba32(110, 96, 120);
        if (floorTint is { } tint)
        {
            floor = tint;
        }

        var floorPoly = scene.AddPlatform(minX, minZ, maxX, maxZ, floor);

        var northShape = PlanarScenePrimitives.Rectangle(minX, maxZ, maxX, maxZ + ExtrusionZ);
        var north = new PlanarStaticPolygon(northShape, face) { DrawFilled = true, SortKey = 10 };
        scene.StaticPolygons.Add(north);
        scene.Collision.AddStatic(new PlanarCollider(northShape));

        var east = PlanarScenePrimitives.Rectangle(maxX, minZ, maxX + ExtrusionX, maxZ + ExtrusionZ);
        scene.StaticPolygons.Add(new PlanarStaticPolygon(east, lip) { DrawFilled = true, SortKey = 11 });

        var cap = PlanarScenePrimitives.Rectangle(minX, minZ, maxX, maxZ);
        var blocker = new PlanarCollider(cap);
        scene.Collision.AddStatic(blocker);
        return (floorPoly, north, blocker);
    }

    public static void AddDoorFrame(PlanarScene scene, float minX, float minZ, float maxX, float maxZ)
    {
        var frame = new Rgba32(40, 80, 140);
        var poly = PlanarScenePrimitives.Rectangle(minX, minZ, maxX, maxZ);
        scene.StaticPolygons.Add(new PlanarStaticPolygon(poly, frame)
        {
            DrawFilled = false,
            DrawOutline = true,
            SortKey = 5,
        });
    }

    public static void AddSecretCrack(PlanarScene scene, float x, float z)
    {
        var crack = new Rgba32(90, 70, 50, 180);
        var poly = PlanarScenePrimitives.Rectangle(x, z, x + 1.2f, z + 0.15f);
        scene.StaticPolygons.Add(new PlanarStaticPolygon(poly, crack) { DrawFilled = true, SortKey = 4 });
    }
}
