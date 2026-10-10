using System.Numerics;
using FrankMoat.Actors;
using FrankMoat.Game;
using Novolis.Math.Geometry;
using Novolis.Rendering.Planar;

namespace FrankMoat.Levels;

internal static class MinigunRange
{
    public static List<RaisedWall> Build(PlanarScene scene, RangeWorld world)
    {
        scene.StaticPolygons.Clear();
        scene.Collision.Clear();
        scene.Sprites.Clear();
        scene.AnimatedSprites.Clear();

        var walls = new List<RaisedWall>();
        var h = RangeBounds.WallHeight;
        var s = RangeBounds.Size;

        walls.Add(WallFactory.Box(-1.4f, -1.4f, s + 1.4f, 0f, h, WallMaterial.Concrete));
        walls.Add(WallFactory.Box(-1.4f, s, s + 1.4f, s + 1.4f, h, WallMaterial.Concrete));
        walls.Add(WallFactory.Box(-1.4f, 0f, 0f, s, h, WallMaterial.Concrete));
        walls.Add(WallFactory.Box(s, 0f, s + 1.4f, s, h, WallMaterial.Concrete));

        walls.Add(WallFactory.Polygon(
            [
                Vector3PlanarExtensions.Xz(7f, 27f),
                Vector3PlanarExtensions.Xz(21f, 40f),
                Vector3PlanarExtensions.Xz(23.2f, 38.2f),
                Vector3PlanarExtensions.Xz(9.2f, 25.2f),
            ],
            RangeBounds.BermHeight,
            WallMaterial.FilthyGlow));

        walls.Add(WallFactory.Box(16.5f, 20.6f, 21.5f, 22.0f, 1.4f, WallMaterial.Steel));
        walls.Add(WallFactory.Box(31.2f, 15.4f, 34.6f, 16.3f, 1.1f, WallMaterial.Concrete));
        walls.Add(WallFactory.Box(38.4f, 41.6f, 46.4f, 43.2f, 2.2f, WallMaterial.Steel));
        walls.Add(WallFactory.Box(28.2f, 8.6f, 30.4f, 9.4f, 1.6f, WallMaterial.Glass));

        foreach (var wall in walls)
        {
            scene.Collision.AddStatic(new PlanarCollider(wall.Footprint));
        }

        world.Walls.Clear();
        world.Walls.AddRange(walls);
        world.Segments.Clear();
        foreach (var wall in walls)
        {
            world.Segments.AddRange(wall.Segments);
        }

        Seed(world);
        return walls;
    }

    private static void Seed(RangeWorld world)
    {
        world.Frank.Position = new Vector3(25f, 0f, 12f);
        world.Frank.Health = 100;
        world.Frank.IFrames = 0f;
        world.Pickups.Add(new RangePickup { Kind = PickupKind.Ammo45, Position = new Vector3(12.4f, 0f, 14.2f) });
        world.Pickups.Add(new RangePickup { Kind = PickupKind.Pump12, Position = new Vector3(13.6f, 0f, 14.2f) });
        world.Pickups.Add(new RangePickup { Kind = PickupKind.Ammo12, Position = new Vector3(32.8f, 0f, 17.2f) });
        world.Pickups.Add(new RangePickup { Kind = PickupKind.Rotary, Position = new Vector3(40.4f, 0f, 18.4f) });
        world.Pickups.Add(new RangePickup { Kind = PickupKind.Ammo762, Position = new Vector3(41.6f, 0f, 18.4f) });
        world.Pickups.Add(new RangePickup { Kind = PickupKind.Health, Position = new Vector3(8.5f, 0f, 10.5f) });
        world.Barrels.Add(new RangeBarrel { Position = new Vector3(14.2f, 0f, 20.4f) });
        world.Barrels.Add(new RangeBarrel { Position = new Vector3(38.6f, 0f, 28.2f) });
        world.Barrels.Add(new RangeBarrel { Position = new Vector3(22.0f, 0f, 33.5f) });
    }
}
