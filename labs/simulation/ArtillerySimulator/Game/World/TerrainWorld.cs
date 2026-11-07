using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Physics.Abstractions;
using Novolis.Physics.Collision.Simple;
using Novolis.Simulation.World;
using Novolis.Simulation.World.Builders;

namespace ArtillerySimulator.Game;

/// <summary>Heightfield range. Real ground is wire-only Kristiansand DEM; flat range keeps procedural wires.</summary>
internal sealed class TerrainWorld
{
    private static readonly Color BoundaryColor = Color.FromArgb(255, 200, 180, 90);
    private static readonly Color WireLow = Color.FromArgb(255, 58, 88, 52);
    private static readonly Color WireHigh = Color.FromArgb(255, 88, 132, 68);

    /// <summary>
    /// Display mesh density — match the 1025 NDH heightfield so flats keep real undulation
    /// (~23 m/cell). Collision stays at 512 for ballistics cost.
    /// </summary>
    public const int DisplayCells = 1024;

    private WorldExtentOptions _extentOptions = new()
    {
        ExtentMeters = KristiansandHeightSampler.EastMeters,
        ExtentNorthMeters = KristiansandHeightSampler.NorthMeters,
        CollisionCells = 512,
        DrawCells = DisplayCells,
    };

    private BoundedHeightfield _field = null!;
    private IHeightSampler _sampler = null!;
    private BvhStaticWorld _collision = null!;
    private Vector3[] _drawVertices = [];
    private WorldLineVertex[] _cachedWires = [];
    private int _drawCells;
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
    public ReadOnlySpan<WorldLineVertex> CachedWires => _cachedWires;

    public void Rebuild(int seed, bool flat, TerrainStyle style = TerrainStyle.Rugged)
    {
        _flat = flat;
        _style = style;
        _extentOptions = new WorldExtentOptions
        {
            ExtentMeters = KristiansandHeightSampler.EastMeters,
            ExtentNorthMeters = KristiansandHeightSampler.NorthMeters,
            CollisionCells = 512,
            DrawCells = DisplayCells,
        };

        _sampler = flat
            ? new AppHeightSampler(seed, true, style)
            : new KristiansandHeightSampler();
        _field = new BoundedHeightfield(_sampler, new AxisAlignedRangeBox(EastMeters, NorthMeters));

        var mesh = HeightfieldMeshBuilder.Build(_sampler, _extentOptions);
        _collision = mesh.Collision;
        _drawVertices = mesh.DrawVertices;
        _drawCells = mesh.DrawCells;

        _cachedWires = RealGround
            ? KristiansandLandWireGrid.Build(_drawVertices, _drawCells, _drawCells)
            : BuildFlatWires();

        GunBaseline = new Vector3(
            SimulationUnits.GunPivotX,
            _field.SampleHeight(SimulationUnits.GunPivotX, NorthMeters * 0.5f) + SimulationUnits.GunHeightOffset,
            NorthMeters * 0.5f);
        if (RealGround)
        {
            KristiansandMapFeatures.ResetClipCache();
            KristiansandPdfRoads.Reset();
        }
    }

    public void PlaceGun(float x, float z)
    {
        x = Math.Clamp(x, 0f, EastMeters);
        z = Math.Clamp(z, 0f, NorthMeters);
        GunBaseline = new Vector3(x, _field.SampleHeight(x, z) + SimulationUnits.GunHeightOffset, z);
    }

    public void DrawDynamic(SilkWireBatch wires)
    {
        DrawRangeBoundary(wires);
    }

    private WorldLineVertex[] BuildFlatWires()
    {
        var batch = new SilkWireBatch();
        for (var z = 0; z <= _drawCells; z++)
        {
            for (var x = 0; x < _drawCells; x++)
            {
                var i0 = z * (_drawCells + 1) + x;
                batch.Line(_drawVertices[i0], _drawVertices[i0 + 1], WireLow);
            }
        }

        for (var x = 0; x <= _drawCells; x++)
        {
            for (var z = 0; z < _drawCells; z++)
            {
                var i0 = z * (_drawCells + 1) + x;
                batch.Line(_drawVertices[i0], _drawVertices[i0 + _drawCells + 1], WireHigh);
            }
        }

        return batch.AsSpan().ToArray();
    }

    private void DrawRangeBoundary(SilkWireBatch wires)
    {
        var east = EastMeters;
        var north = NorthMeters;
        var y = MathF.Max(520f, ExtentMeters * 0.03f);
        var a = new Vector3(0f, y, 0f);
        var b = new Vector3(east, y, 0f);
        var c = new Vector3(east, y, north);
        var d = new Vector3(0f, y, north);
        wires.Line(a, b, BoundaryColor);
        wires.Line(b, c, BoundaryColor);
        wires.Line(c, d, BoundaryColor);
        wires.Line(d, a, BoundaryColor);
    }
}
