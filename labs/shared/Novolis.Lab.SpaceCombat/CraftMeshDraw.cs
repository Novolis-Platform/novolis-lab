using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Simulation.SpaceCombat;

namespace Novolis.Lab.SpaceCombat;

public static class CraftMeshDraw
{
    public static void DrawCraft(
        RayGameContext ctx,
        CraftState craft,
        MeshData? mesh,
        Color hull,
        Color accent)
    {
        if (!craft.Active)
            return;

        if (mesh is null || mesh.Indices.Length < 3)
        {
            DrawProxy(ctx, craft, hull, accent);
            return;
        }

        // Wireframe sample of triangles (MVP — full shaded mesh later)
        var pos = craft.Position;
        var scale = craft.Profile.Role == CraftRole.Freighter ? 2.2f : 1f;
        var count = Math.Min(mesh.Indices.Length / 3, 400);
        for (var t = 0; t < count; t++)
        {
            var i0 = mesh.Indices[t * 3];
            var i1 = mesh.Indices[t * 3 + 1];
            var i2 = mesh.Indices[t * 3 + 2];
            var a = ReadVertex(mesh.Positions, i0) * scale + pos;
            var b = ReadVertex(mesh.Positions, i1) * scale + pos;
            var c = ReadVertex(mesh.Positions, i2) * scale + pos;
            ctx.DrawLaserBolt(a, b, accent);
            ctx.DrawLaserBolt(b, c, accent);
            ctx.DrawLaserBolt(c, a, hull);
        }
    }

    private static Vector3 ReadVertex(float[] positions, int index)
    {
        var i = index * 3;
        if (i + 2 >= positions.Length)
            return Vector3.Zero;
        return new Vector3(positions[i], positions[i + 1], positions[i + 2]);
    }

    private static void DrawProxy(RayGameContext ctx, CraftState craft, Color hull, Color accent)
    {
        var size = craft.Profile.Role switch
        {
            CraftRole.Freighter => new Vector3(10f, 4f, 14f),
            CraftRole.Hostile => new Vector3(3.5f, 2.2f, 3.8f),
            _ => new Vector3(5.5f, 1.8f, 7.5f),
        };
        ctx.DrawShipBox(craft.Position, size, hull);
        ctx.DrawShipWires(craft.Position, size * 1.05f, accent);
        if (craft.Profile.Role == CraftRole.Hostile)
        {
            var right = Vector3.Normalize(Vector3.Cross(craft.Forward, Vector3.UnitY));
            if (right.LengthSquared() < 1e-4f)
                right = Vector3.UnitX;
            ctx.DrawShipBox(craft.Position + right * 3.2f, new Vector3(0.15f, 2.6f, 3.2f), accent);
            ctx.DrawShipBox(craft.Position - right * 3.2f, new Vector3(0.15f, 2.6f, 3.2f), accent);
        }
    }
}
