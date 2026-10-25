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
    private static readonly Color BoundaryColor = Color.FromArgb(255, 200, 180, 90);
    private static readonly Color GroundFill = Color.FromArgb(28, 22, 38, 30);
    private static readonly Color WireLow = Color.FromArgb(255, 58, 88, 52);
    private static readonly Color WireHigh = Color.FromArgb(255, 88, 132, 68);

    private readonly WorldExtentOptions _extentOptions = new()
    {
        ExtentMeters = SimulationUnits.ExtentMeters,
        CollisionCells = 256,
        DrawCells = 128,
    };

    private BoundedHeightfield _field = null!;
    private BvhStaticWorld _collision = null!;
    private Vector3[] _drawVertices = [];
    private int _drawCells;
    private int _seed;
    private bool _flat;
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

        GunBaseline = new Vector3(
            SimulationUnits.GunPivotX,
            _field.SampleHeight(SimulationUnits.GunPivotX, ExtentMeters * 0.5f) + SimulationUnits.GunHeightOffset,
            ExtentMeters * 0.5f);
    }

    public void Draw(RayGameContext ctx)
    {
        var e = ExtentMeters;
        ctx.DrawPlane(new Vector3(e * 0.5f, 0f, e * 0.5f), new Vector2(e, e), GroundFill);
        ctx.DrawHeightfieldWires(_drawVertices, _drawCells, _drawCells, WireLow, WireHigh);
        DrawRangeBoundary(ctx);
    }

    private void DrawRangeBoundary(RayGameContext ctx)
    {
        var e = ExtentMeters;
        var y = MathF.Max(520f, ExtentMeters * 0.03f);
        var a = new Vector3(0f, y, 0f);
        var b = new Vector3(e, y, 0f);
        var c = new Vector3(e, y, e);
        var d = new Vector3(0f, y, e);
        ctx.DrawBolt(a, b, BoundaryColor);
        ctx.DrawBolt(b, c, BoundaryColor);
        ctx.DrawBolt(c, d, BoundaryColor);
        ctx.DrawBolt(d, a, BoundaryColor);
    }
}
