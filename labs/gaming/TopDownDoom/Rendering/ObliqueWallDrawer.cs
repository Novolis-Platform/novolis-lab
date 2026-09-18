using Novolis.Math.Geometry;
using Novolis.Rendering.TwoD;

namespace TopDownDoom.Game;

/// <summary>
/// Draws walls with a slight oblique extrusion so doors and corners read at ~70° tilt without a 3D camera.
/// </summary>
internal static class ObliqueWallDrawer
{
    private const float ExtrusionZ = 0.55f;
    private const float ExtrusionX = 0.18f;

    public static (TwoDStaticPolygon Floor, TwoDStaticPolygon North, TwoDCollider Blocker) AddWall(
        TwoDScene scene,
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

        var northShape = TwoDScenePrimitives.Rectangle(minX, maxZ, maxX, maxZ + ExtrusionZ);
        var north = new TwoDStaticPolygon(northShape, face) { DrawFilled = true, SortKey = 10 };
        scene.StaticPolygons.Add(north);
        scene.Collision.AddStatic(new TwoDCollider(northShape));

        var east = TwoDScenePrimitives.Rectangle(maxX, minZ, maxX + ExtrusionX, maxZ + ExtrusionZ);
        scene.StaticPolygons.Add(new TwoDStaticPolygon(east, lip) { DrawFilled = true, SortKey = 11 });

        var cap = TwoDScenePrimitives.Rectangle(minX, minZ, maxX, maxZ);
        var blocker = new TwoDCollider(cap);
        scene.Collision.AddStatic(blocker);
        return (floorPoly, north, blocker);
    }

    public static void AddDoorFrame(TwoDScene scene, float minX, float minZ, float maxX, float maxZ)
    {
        var frame = new Rgba32(40, 80, 140);
        var poly = TwoDScenePrimitives.Rectangle(minX, minZ, maxX, maxZ);
        scene.StaticPolygons.Add(new TwoDStaticPolygon(poly, frame)
        {
            DrawFilled = false,
            DrawOutline = true,
            SortKey = 5,
        });
    }

    public static void AddSecretCrack(TwoDScene scene, float x, float z)
    {
        var crack = new Rgba32(90, 70, 50, 180);
        var poly = TwoDScenePrimitives.Rectangle(x, z, x + 1.2f, z + 0.15f);
        scene.StaticPolygons.Add(new TwoDStaticPolygon(poly, crack) { DrawFilled = true, SortKey = 4 });
    }
}
