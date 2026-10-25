using System.Drawing;
using System.Numerics;
using Novolis.Physics.Collision.Simple;
using Novolis.Raylib.Game;
using Novolis.Simulation.World;
using Novolis.Simulation.World.Builders;

namespace ArtillerySimulator.Game;

/// <summary>Procedural heightfield with logical range box and ground-projected edge impacts.</summary>
internal sealed class TerrainWorld
{
    private static readonly Color StakeColor = Color.FromArgb(180, 210, 170, 110);
    private static readonly Color CorridorColor = Color.FromArgb(110, 230, 170, 90);

    private readonly WorldExtentOptions _extentOptions = new()
    {
        ExtentMeters = SimulationUnits.ExtentMeters,
        CollisionCells = 256,
        DrawCells = 72,
    };

    private BoundedHeightfield _field = null!;
    private BvhStaticWorld _collision = null!;
    private Vector3[] _drawVertices = [];
    private int _drawCells;
    private int _seed;
    private bool _flat;
    private float _minHeight;
    private float _maxHeight = 1f;
    private TerrainStyle _style = TerrainStyle.Rugged;

    public float ExtentMeters => _extentOptions.ExtentMeters;
    public BoundedHeightfield Field => _field;
    public BvhStaticWorld Collision => _collision;
    public Vector3 GunBaseline { get; private set; }
    public bool IsFlat => _flat;
    public TerrainStyle Style => _style;

    public void Rebuild(int seed, bool flat, TerrainStyle style = TerrainStyle.Rugged)
    {
        _seed = seed;
        _flat = flat;
        _style = style;

        var sampler = new AppHeightSampler(seed, flat, style);
        _field = new BoundedHeightfield(sampler, ExtentMeters);

        var mesh = HeightfieldMeshBuilder.Build(sampler, _extentOptions);
        _collision = mesh.Collision;
        _drawVertices = mesh.DrawVertices;
        _drawCells = mesh.DrawCells;
        _minHeight = float.MaxValue;
        _maxHeight = float.MinValue;
        foreach (var vertex in _drawVertices)
        {
            _minHeight = MathF.Min(_minHeight, vertex.Y);
            _maxHeight = MathF.Max(_maxHeight, vertex.Y);
        }

        if (_maxHeight - _minHeight < 1f)
            _maxHeight = _minHeight + 1f;

        GunBaseline = new Vector3(
            SimulationUnits.GunPivotX,
            _field.SampleHeight(SimulationUnits.GunPivotX, ExtentMeters * 0.5f) + SimulationUnits.GunHeightOffset,
            ExtentMeters * 0.5f);
    }

    public void Draw(RayGameContext ctx)
    {
        var e = ExtentMeters;
        var (low, _, _) = Palette();
        var fill = Color.FromArgb(50, low.R, low.G, low.B);
        ctx.DrawPlane(new Vector3(e * 0.5f, 0f, e * 0.5f), new Vector2(e, e), fill);
        if (_flat)
            DrawLattice(ctx);
        else
            DrawTintedWires(ctx);
        DrawCornerPosts(ctx);
    }

    public void DrawAimCorridor(RayGameContext ctx, Vector3 gunBaseline, float azimuthDegrees, Vector3 predictedEnd)
    {
        var azimuth = azimuthDegrees * (MathF.PI / 180f);
        var forward = new Vector3(MathF.Cos(azimuth), 0f, MathF.Sin(azimuth));
        var right = new Vector3(-forward.Z, 0f, forward.X);
        var origin = new Vector3(gunBaseline.X, 0f, gunBaseline.Z);
        var reach = Vector3.Distance(new Vector3(predictedEnd.X, 0f, predictedEnd.Z), origin);
        if (reach < 200f)
            reach = ExtentMeters * 0.75f;

        for (var distance = 2000f; distance < ExtentMeters - 200f; distance += 2000f)
        {
            var flat = origin + forward * distance;
            if (!Field.IsInside(flat.X, flat.Z))
                continue;

            var basePoint = new Vector3(flat.X, Field.SampleHeight(flat.X, flat.Z), flat.Z);
            var head = basePoint + Vector3.UnitY * 55f;
            ctx.DrawBolt(basePoint, head, StakeColor);
            ctx.DrawBolt(head - right * 10f, head + right * 10f, StakeColor);
        }

        var dash = 220f;
        var gap = 180f;
        for (var distance = 80f; distance < reach; distance += dash + gap)
        {
            var a = origin + forward * distance;
            var b = origin + forward * MathF.Min(distance + dash, reach);
            if (!Field.IsInside(a.X, a.Z) || !Field.IsInside(b.X, b.Z))
                break;

            var from = new Vector3(a.X, Field.SampleHeight(a.X, a.Z) + 2f, a.Z);
            var to = new Vector3(b.X, Field.SampleHeight(b.X, b.Z) + 2f, b.Z);
            ctx.DrawBolt(from, to, CorridorColor);
        }

        var mark = origin + forward * MathF.Min(reach, ExtentMeters - 50f);
        if (!Field.IsInside(mark.X, mark.Z))
            return;

        var center = new Vector3(mark.X, Field.SampleHeight(mark.X, mark.Z) + 3f, mark.Z);
        var column = Math.Clamp(reach * 0.08f, 160f, 640f);
        ctx.DrawBolt(center, center + Vector3.UnitY * column, Color.FromArgb(230, 255, 176, 86));
        ctx.DrawGlowSphere(center + Vector3.UnitY * column, 18f, Color.FromArgb(230, 255, 176, 86));
        DrawGroundRing(ctx, center, Math.Clamp(reach * 0.02f, 40f, 180f), CorridorColor);
    }

    public void DrawGunPad(RayGameContext ctx, Vector3 gunBaseline)
    {
        const float half = 40f;
        const float step = 8f;
        var color = Color.FromArgb(190, 168, 156, 120);
        var originX = gunBaseline.X;
        var originZ = gunBaseline.Z;
        for (var offset = -half; offset <= half; offset += step)
        {
            var x0 = originX + offset;
            var z0 = originZ - half;
            var z1 = originZ + half;
            if (Field.IsInside(x0, z0) && Field.IsInside(x0, z1))
            {
                ctx.DrawBolt(
                    new Vector3(x0, Field.SampleHeight(x0, z0) + 0.4f, z0),
                    new Vector3(x0, Field.SampleHeight(x0, z1) + 0.4f, z1),
                    color);
            }

            var z = originZ + offset;
            var xStart = originX - half;
            var xEnd = originX + half;
            if (Field.IsInside(xStart, z) && Field.IsInside(xEnd, z))
            {
                ctx.DrawBolt(
                    new Vector3(xStart, Field.SampleHeight(xStart, z) + 0.4f, z),
                    new Vector3(xEnd, Field.SampleHeight(xEnd, z) + 0.4f, z),
                    color);
            }
        }
    }

    private void DrawTintedWires(RayGameContext ctx)
    {
        var cells = _drawCells;
        var stride = cells + 1;
        for (var z = 0; z < cells; z++)
        {
            for (var x = 0; x < cells; x++)
            {
                var i00 = z * stride + x;
                var a = _drawVertices[i00];
                var b = _drawVertices[i00 + 1];
                var c = _drawVertices[i00 + stride];
                ctx.DrawBolt(a, b, Tint((a.Y + b.Y) * 0.5f));
                ctx.DrawBolt(a, c, Tint((a.Y + c.Y) * 0.5f));
            }
        }
    }

    private void DrawLattice(RayGameContext ctx)
    {
        var color = Tint(_minHeight);
        var e = ExtentMeters;
        for (var step = 0f; step <= e; step += 2000f)
        {
            var y = 1.5f;
            ctx.DrawBolt(new Vector3(step, y, 0f), new Vector3(step, y, e), color);
            ctx.DrawBolt(new Vector3(0f, y, step), new Vector3(e, y, step), color);
        }
    }

    private void DrawCornerPosts(RayGameContext ctx)
    {
        var e = ExtentMeters;
        var posts = new[]
        {
            new Vector3(40f, 0f, 40f),
            new Vector3(e - 40f, 0f, 40f),
            new Vector3(e - 40f, 0f, e - 40f),
            new Vector3(40f, 0f, e - 40f),
        };
        foreach (var post in posts)
        {
            var y = Field.SampleHeight(post.X, post.Z);
            var basePoint = new Vector3(post.X, y, post.Z);
            ctx.DrawBolt(basePoint, basePoint + Vector3.UnitY * 160f, StakeColor);
        }
    }

    private Color Tint(float height)
    {
        var t = Math.Clamp((height - _minHeight) / (_maxHeight - _minHeight), 0f, 1f);
        var (low, mid, high) = Palette();
        return t < 0.5f ? Lerp(low, mid, t * 2f) : Lerp(mid, high, (t - 0.5f) * 2f);
    }

    private (Color Low, Color Mid, Color High) Palette() => _style switch
    {
        TerrainStyle.AfghanHighland => (
            Color.FromArgb(255, 132, 96, 58),
            Color.FromArgb(255, 176, 122, 62),
            Color.FromArgb(255, 214, 186, 140)),
        TerrainStyle.NordicRidges => (
            Color.FromArgb(255, 36, 72, 82),
            Color.FromArgb(255, 92, 128, 132),
            Color.FromArgb(255, 206, 214, 216)),
        _ => (
            Color.FromArgb(255, 24, 52, 34),
            Color.FromArgb(255, 118, 132, 58),
            Color.FromArgb(255, 214, 196, 132)),
    };

    private static Color Lerp(Color a, Color b, float t) =>
        Color.FromArgb(
            255,
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));

    private static void DrawGroundRing(RayGameContext ctx, Vector3 center, float radius, Color color)
    {
        const int segments = 20;
        var previous = center + new Vector3(radius, 0f, 0f);
        for (var i = 1; i <= segments; i++)
        {
            var angle = i / (float)segments * MathF.Tau;
            var next = center + new Vector3(MathF.Cos(angle) * radius, 0f, MathF.Sin(angle) * radius);
            ctx.DrawBolt(previous, next, color);
            previous = next;
        }
    }
}
