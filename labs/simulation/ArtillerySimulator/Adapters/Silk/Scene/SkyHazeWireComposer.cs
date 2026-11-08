using System.Drawing;
using System.Numerics;
using Novolis.Silk;

namespace ArtillerySimulator;

internal static class SkyHazeWireComposer
{
    public static void Draw(SilkWireBatch wires, Vector3 camera, TerrainWorld terrain)
    {
        var light = new Vector3(
            terrain.ExtentMeters * 0.55f,
            6800f,
            terrain.GunBaseline.Z - 4000f);
        var toLight = Vector3.Normalize(light - camera);
        var origin = camera + toLight * 2200f;
        for (var i = 0; i < 10; i++)
        {
            var t = i / 9f;
            var lateral = Vector3.Normalize(Vector3.Cross(toLight, Vector3.UnitY));
            if (lateral.LengthSquared() < 1e-4f)
                lateral = Vector3.UnitZ;
            var offset = lateral * ((t - 0.5f) * 900f) + Vector3.UnitY * ((t - 0.5f) * 120f);
            var a = origin + offset;
            var b = a + toLight * (4800f + t * 2200f);
            var alpha = (int)(28 + 22 * (1f - MathF.Abs(t - 0.5f) * 2f));
            wires.Line(a, b, Color.FromArgb(alpha, 255, 200, 140));
        }

        var gun = terrain.GunBaseline;
        for (var r = 2; r <= 5; r++)
        {
            var radius = r * 1800f;
            var alpha = 18 + r * 4;
            wires.Ring(
                new Vector3(gun.X, gun.Y + 40f + r * 12f, gun.Z),
                radius,
                Color.FromArgb(alpha, 180, 150, 110),
                segments: 40,
                yLift: 0f);
        }
    }
}
