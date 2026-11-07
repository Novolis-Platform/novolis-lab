using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Physics.Abstractions;
using Novolis.Physics.Collision.Simple;
using Novolis.Silk;
using Novolis.Simulation.World;
using Novolis.Simulation.World.Builders;

namespace ArtillerySimulator;

/// <summary>Kristiansand NDH heightfield range — wire terrain + primary-road overlay.</summary>
internal sealed class TerrainWorld : IDisposable
{
    private static readonly Color BoundaryColor = Color.FromArgb(255, 200, 180, 90);

    /// <summary>
    /// Display mesh density — match the 1025 NDH heightfield (~23 m/cell).
    /// Collision stays at 512 for ballistics cost.
    /// </summary>
    public const int DisplayCells = 1024;

    /// <summary>Every Nth display cell emits a wire (2 ≈ 4× fewer lines than full 1024 grid).</summary>
    public const int WireCellStride = 2;

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
    private WorldLineVertex[] _cachedRoadWires = [];
    private WorldLineGpuMesh? _terrainMesh;
    private WorldLineGpuMesh? _roadMesh;
    private int _drawCells;

    public float ExtentMeters => KristiansandHeightSampler.MapExtentMeters;
    public float EastMeters => KristiansandHeightSampler.EastMeters;
    public float NorthMeters => KristiansandHeightSampler.NorthMeters;
    public BoundedHeightfield Field => _field;
    public BvhStaticWorld Collision => _collision;
    public Vector3 GunBaseline { get; private set; }
    public ReadOnlySpan<WorldLineVertex> CachedWires => _cachedWires;
    public int TerrainLineCount => _cachedWires.Length / 2;
    public int RoadLineCount => _cachedRoadWires.Length / 2;

    public void Rebuild()
    {
        InvalidateGpuMeshes();
        _extentOptions = new WorldExtentOptions
        {
            ExtentMeters = KristiansandHeightSampler.EastMeters,
            ExtentNorthMeters = KristiansandHeightSampler.NorthMeters,
            CollisionCells = 512,
            DrawCells = DisplayCells,
        };

        _sampler = new KristiansandHeightSampler();
        _field = new BoundedHeightfield(_sampler, new AxisAlignedRangeBox(EastMeters, NorthMeters));

        var mesh = HeightfieldMeshBuilder.Build(_sampler, _extentOptions);
        _collision = mesh.Collision;
        _drawVertices = mesh.DrawVertices;
        _drawCells = mesh.DrawCells;

        _cachedWires = KristiansandLandWireGrid.Build(_drawVertices, _drawCells, _drawCells, WireCellStride);

        KristiansandMapFeatures.ResetClipCache();
        KristiansandNvdbRoads.Reset();
        _cachedRoadWires = KristiansandNvdbRoads.Build(_sampler);

        GunBaseline = new Vector3(
            SimulationUnits.GunPivotX,
            _field.SampleHeight(SimulationUnits.GunPivotX, NorthMeters * 0.5f) + SimulationUnits.GunHeightOffset,
            NorthMeters * 0.5f);
    }

    public void PlaceGun(float x, float z)
    {
        x = Math.Clamp(x, 0f, EastMeters);
        z = Math.Clamp(z, 0f, NorthMeters);
        GunBaseline = new Vector3(x, _field.SampleHeight(x, z) + SimulationUnits.GunHeightOffset, z);
    }

    /// <summary>Uploads static terrain (+ roads) once; redraws from GPU each frame.</summary>
    public void DrawStatic(SilkFrame frame, in Matrix4x4 viewProjection)
    {
        EnsureGpuMeshes(frame);
        if (_terrainMesh is not null)
            frame.DrawWorldLineMesh(_terrainMesh, viewProjection);
        if (_roadMesh is not null)
            frame.DrawWorldLineMesh(_roadMesh, viewProjection);
    }

    public void DrawDynamic(SilkWireBatch wires)
    {
        DrawRangeBoundary(wires);
    }

    public void Dispose() => InvalidateGpuMeshes();

    private void EnsureGpuMeshes(SilkFrame frame)
    {
        _terrainMesh ??= _cachedWires.Length >= 2
            ? frame.CreateWorldLineMesh(_cachedWires)
            : null;
        if (_roadMesh is null && _cachedRoadWires.Length >= 2)
            _roadMesh = frame.CreateWorldLineMesh(_cachedRoadWires);
    }

    private void InvalidateGpuMeshes()
    {
        _terrainMesh?.Dispose();
        _terrainMesh = null;
        _roadMesh?.Dispose();
        _roadMesh = null;
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
