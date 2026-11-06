using System.Drawing;
using System.Numerics;
using Novolis.Physics.Abstractions;
using Novolis.Physics.Collision.Simple;
using Novolis.Raylib.Game;
using Novolis.Raylib.Rendering;
using Novolis.Simulation.World;
using Novolis.Simulation.World.Builders;

namespace ArtillerySimulator.Game;

/// <summary>Heightfield range. Wires stay the original mesh. Heights are the Kristiansand grid unless the range is flat.</summary>
internal sealed class TerrainWorld
{
    private static readonly Color BoundaryColor = Color.FromArgb(255, 200, 180, 90);
    private static readonly Color GroundFill = Color.FromArgb(28, 22, 38, 30);
    private static readonly Color WireLow = Color.FromArgb(255, 58, 88, 52);
    private static readonly Color WireHigh = Color.FromArgb(255, 88, 132, 68);

    private WorldExtentOptions _extentOptions = new()
    {
        ExtentMeters = KristiansandHeightSampler.EastMeters,
        ExtentNorthMeters = KristiansandHeightSampler.NorthMeters,
        CollisionCells = 512,
        DrawCells = 512,
    };

    private BoundedHeightfield _field = null!;
    private BvhStaticWorld _collision = null!;
    private Vector3[] _drawVertices = [];
    private int _drawCells;
    private int _seed;
    private bool _flat;
    private TerrainStyle _style = TerrainStyle.Rugged;

    public float ExtentMeters => KristiansandHeightSampler.MapExtentMeters;

    public float EastMeters => KristiansandHeightSampler.EastMeters;

    public float NorthMeters => KristiansandHeightSampler.NorthMeters;
    public BoundedHeightfield Field => _field;
    public BvhStaticWorld Collision => _collision;
    public Vector3 GunBaseline { get; private set; }
    public bool IsFlat => _flat;
    public bool RealGround => !_flat;
    public TerrainStyle Style => _style;

    public void Rebuild(int seed, bool flat, TerrainStyle style = TerrainStyle.Rugged)
    {
        _seed = seed;
        _flat = flat;
        _style = style;
        _extentOptions = new WorldExtentOptions
        {
            ExtentMeters = KristiansandHeightSampler.EastMeters,
            ExtentNorthMeters = KristiansandHeightSampler.NorthMeters,
            CollisionCells = 512,
            DrawCells = 512,
        };

        IHeightSampler sampler = flat
            ? new AppHeightSampler(seed, true, style)
            : new KristiansandHeightSampler();
        _field = new BoundedHeightfield(sampler, new AxisAlignedRangeBox(EastMeters, NorthMeters));

        var mesh = HeightfieldMeshBuilder.Build(sampler, _extentOptions);
        _collision = mesh.Collision;
        _drawVertices = mesh.DrawVertices;
        _drawCells = mesh.DrawCells;

        GunBaseline = new Vector3(
            SimulationUnits.GunPivotX,
            _field.SampleHeight(SimulationUnits.GunPivotX, NorthMeters * 0.5f) + SimulationUnits.GunHeightOffset,
            NorthMeters * 0.5f);
        if (RealGround)
            KristiansandMapFeatures.ResetClipCache();
    }

    public void PlaceGun(float x, float z)
    {
        x = Math.Clamp(x, 0f, EastMeters);
        z = Math.Clamp(z, 0f, NorthMeters);
        GunBaseline = new Vector3(x, _field.SampleHeight(x, z) + SimulationUnits.GunHeightOffset, z);
    }

    public void Draw(RayGameContext ctx, Camera camera)
    {
        if (RealGround)
        {
            KristiansandReferenceMap.TryDraw(ctx);
            // Ultra-high-fidelity wire overlay on the draped DEM (512×512).
            ctx.DrawHeightfieldWires(_drawVertices, _drawCells, _drawCells, WireLow, WireHigh);
        }
        else
        {
            var east = EastMeters;
            var north = NorthMeters;
            ctx.DrawPlane(new Vector3(east * 0.5f, 0f, north * 0.5f), new Vector2(east, north), GroundFill);
            ctx.DrawHeightfieldWires(_drawVertices, _drawCells, _drawCells, WireLow, WireHigh);
        }

        DrawRangeBoundary(ctx);
    }

    private void DrawRangeBoundary(RayGameContext ctx)
    {
        var east = EastMeters;
        var north = NorthMeters;
        var y = KristiansandReferenceMap.IsActive ? 12f : MathF.Max(520f, ExtentMeters * 0.03f);
        var a = new Vector3(0f, y, 0f);
        var b = new Vector3(east, y, 0f);
        var c = new Vector3(east, y, north);
        var d = new Vector3(0f, y, north);
        ctx.DrawBolt(a, b, BoundaryColor);
        ctx.DrawBolt(b, c, BoundaryColor);
        ctx.DrawBolt(c, d, BoundaryColor);
        ctx.DrawBolt(d, a, BoundaryColor);
    }
}
