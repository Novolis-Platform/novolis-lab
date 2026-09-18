using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;

namespace XFighter.Game;

/// <summary>Distant capitals, nebula tint, and debris to fill the battlespace.</summary>
internal sealed class BattleSpace
{
    private readonly Vector3[] _asteroids;
    private readonly float[] _asteroidScale;
    private readonly Vector3[] _capitals;
    private readonly Vector3[] _nebulaCenters;
    private readonly Color[] _nebulaColors;

    public BattleSpace(Random rng)
    {
        _asteroids = new Vector3[48];
        _asteroidScale = new float[48];
        for (var i = 0; i < _asteroids.Length; i++)
        {
            _asteroids[i] = new Vector3(
                (float)(rng.NextDouble() * 260 - 130),
                (float)(rng.NextDouble() * 60 - 30),
                (float)(rng.NextDouble() * -180 - 40));
            _asteroidScale[i] = 0.4f + (float)rng.NextDouble() * 1.8f;
        }

        _capitals =
        [
            new(-120f, -8f, -220f),
            new(95f, 12f, -280f),
            new(0f, -18f, -340f),
        ];

        _nebulaCenters =
        [
            new(-60f, 15f, -160f),
            new(70f, -10f, -200f),
        ];
        _nebulaColors =
        [
            Color.FromArgb(35, 80, 40, 140),
            Color.FromArgb(30, 140, 50, 90),
        ];
    }

    public void Draw(RayGameContext ctx, Vector3 playerPos)
    {
        DrawNebulae(ctx, playerPos);
        DrawCapitals(ctx, playerPos);
        DrawAsteroids(ctx, playerPos);
        DrawTrenchGlint(ctx, playerPos);
    }

    private void DrawNebulae(RayGameContext ctx, Vector3 playerPos)
    {
        for (var n = 0; n < _nebulaCenters.Length; n++)
        {
            var c = _nebulaCenters[n] - playerPos * 0.008f;
            var color = _nebulaColors[n];
            for (var ring = 0; ring < 5; ring++)
            {
                var r = 18f + ring * 9f;
                ctx.DrawGlowSphere(c, r, color);
            }
        }
    }

    private void DrawCapitals(RayGameContext ctx, Vector3 playerPos)
    {
        var hull = Color.FromArgb(90, 12, 14, 22);
        var lights = Color.FromArgb(120, 255, 180, 80);
        foreach (var cap in _capitals)
        {
            var p = cap - playerPos * 0.006f;
            ctx.DrawShipBox(p, new Vector3(42f, 8f, 14f), hull);
            ctx.DrawShipWires(p, new Vector3(44f, 10f, 16f), Color.FromArgb(60, 30, 35, 50));
            ctx.DrawGlowSphere(p + new Vector3(0, 2f, 6f), 1.2f, lights);
        }
    }

    private void DrawAsteroids(RayGameContext ctx, Vector3 playerPos)
    {
        var rock = Color.FromArgb(200, 55, 50, 48);
        var rockDark = Color.FromArgb(200, 35, 32, 30);
        foreach (var i in Enumerable.Range(0, _asteroids.Length))
        {
            var p = _asteroids[i] - playerPos * 0.015f;
            var s = _asteroidScale[i];
            ctx.DrawGlowSphere(p, s, rock);
            ctx.DrawGlowSphere(p + new Vector3(s * 0.3f, 0, 0), s * 0.55f, rockDark);
        }
    }

    private static void DrawTrenchGlint(RayGameContext ctx, Vector3 playerPos)
    {
        var baseZ = -140f - playerPos.Z * 0.01f;
        var panel = Color.FromArgb(50, 25, 28, 38);
        ctx.DrawPlane(new Vector3(0, -25f, baseZ), new Vector2(400f, 8f), panel);
        for (var x = -180f; x <= 180f; x += 24f)
        {
            var spark = Color.FromArgb(80, 120, 180, 220);
            ctx.DrawGlowSphere(new Vector3(x, -24f, baseZ + 2f), 0.6f, spark);
        }
    }
}
