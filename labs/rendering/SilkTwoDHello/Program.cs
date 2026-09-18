using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Rendering.Backends.TwoD.Silk;
using Novolis.Rendering.TwoD;
using Novolis.Rendering.Presentation;

namespace SilkTwoDHello;

internal static class Program
{
    public static void Main()
    {
        var playerZ = 2f;
        var velocityZ = 0f;
        var playerPos = Vector3PlanarExtensions.Xz(4f, playerZ);
        const float gravity = 22f;
        const float jumpSpeed = 8f;
        const float radius = 0.35f;
        TwoDStaticPolygon? playerMarker = null;

        SilkTwoDGame.Run("SilkTwoDHello — orthographic 2D", 800, 600, ctx =>
        {
            var scene = ctx.Scene;
            scene.Camera.ClearColor = new Rgba32(30, 36, 52);
            scene.Camera.WorldUnitsPerPixel = 1f / 28f;

            scene.AddPlatform(0f, 0f, 18f, 1.2f, new Rgba32(70, 90, 120));
            scene.AddPlatform(4f, 3f, 8f, 3.8f, new Rgba32(90, 110, 150));
            scene.AddPlatform(10f, 5.5f, 16f, 6.2f, new Rgba32(90, 110, 150));

            scene.Menus.Push(new TwoDMenuScreen("SILK TWO-D HELLO", [
                new TwoDMenuItem("PLAY", Tag: "play", OnSelect: () => { scene.Menus.Pop(); return (object?)"play"; }),
                new TwoDMenuItem("QUIT", Tag: "quit", OnSelect: () => { Environment.Exit(0); return (object?)"quit"; }),
            ]));
        }, ctx =>
        {
            if (ctx.Scene.Menus.IsActive)
            {
                return;
            }

            var scene = ctx.Scene;
            var dt = ctx.DeltaSeconds;

            var move = 0f;
            if (ctx.IsKeyDown(Key.A))
            {
                move -= 1f;
            }

            if (ctx.IsKeyDown(Key.D))
            {
                move += 1f;
            }

            var pos = playerPos;
            var horizontal = new Vector3(move * 4.5f * dt, 0f, 0f);
            pos = scene.Collision.MoveCircle(pos, horizontal, radius);

            var grounded = !scene.Collision.Overlaps(pos + new Vector3(0f, 0f, -(radius + 0.03f)), radius);
            if (grounded && ctx.IsKeyPressed(Key.Space))
            {
                velocityZ = jumpSpeed;
                grounded = false;
            }

            if (!grounded)
            {
                velocityZ -= gravity * dt;
                var vertical = new Vector3(0f, 0f, velocityZ * dt);
                pos = scene.Collision.MoveCircle(pos, vertical, radius);
            }
            else if (velocityZ < 0f)
            {
                velocityZ = 0f;
            }

            playerPos = pos;
            playerZ = pos.Z;

            var targetX = pos.X;
            var t = 1f - MathF.Exp(-8f * dt);
            scene.Camera.Position = Vector3.Lerp(scene.Camera.Position, Vector3PlanarExtensions.Xz(targetX, playerZ + 1.5f), t);

            scene.Update(dt);
            scene.Hud.Elements.Clear();
            scene.Hud.AddText("A/D move  |  Space jump  |  Esc menu", 12, 12, 2f, new Rgba32(210, 220, 235));
            scene.Hud.AddText($"pos {pos.X:F1},{pos.Z:F1}", 12, 36, 2f, new Rgba32(180, 200, 220));

            if (playerMarker is not null)
            {
                scene.StaticPolygons.Remove(playerMarker);
            }

            playerMarker = CreatePlayerMarker(pos, radius);
            scene.StaticPolygons.Add(playerMarker);
        });
    }

    private static TwoDStaticPolygon CreatePlayerMarker(Vector3 pos, float radius)
    {
        var minX = pos.X - radius;
        var maxX = pos.X + radius;
        var minZ = pos.Z - radius;
        var maxZ = pos.Z + radius;
        return new TwoDStaticPolygon(
            TwoDScenePrimitives.Rectangle(minX, minZ, maxX, maxZ),
            new Rgba32(255, 180, 90))
        {
            DrawFilled = true,
            DrawOutline = true,
            SortKey = 1000,
        };
    }
}
