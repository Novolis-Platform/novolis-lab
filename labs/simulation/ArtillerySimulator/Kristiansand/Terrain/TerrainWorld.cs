using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Physics.Abstractions;
using Novolis.Physics.Collision.Simple;
using Novolis.Silk;
using Novolis.Simulation.World;
using Novolis.Simulation.World.Builders;

namespace ArtillerySimulator;

/// <summary>Kristiansand NDH heightfield — solid land/water fill + primary-road overlay.</summary>
internal sealed class TerrainWorld : IDisposable
{
    private static readonly Color BoundaryColor = Color.FromArgb(255, 200, 180, 90);

    /// <summary>
    /// Fill mesh density (512 ≈ 47 m/cell on the long axis). Collision stays coarse for boot.
    /// </summary>
    public const int DisplayCells = 512;

    /// <summary>~188 m/cell — enough for shell/terrain contact without a 500k-tri BVH at boot.</summary>
    public const int CollisionCells = 128;

    private WorldExtentOptions _extentOptions = new()
    {
        ExtentMeters = KristiansandHeightSampler.EastMeters,
        ExtentNorthMeters = KristiansandHeightSampler.NorthMeters,
        CollisionCells = CollisionCells,
        DrawCells = DisplayCells,
    };

    private BoundedHeightfield _field = null!;
    private IHeightSampler _sampler = null!;
    private BvhStaticWorld _collision = null!;
    private Vector3[] _drawVertices = [];
    private WorldSolidVertex[] _cachedFill = [];
    private WorldLineVertex[] _cachedRoadWires = [];
    private WorldSolidGpuMesh? _fillMesh;
    private WorldLineGpuMesh? _roadMesh;
    private int _drawCells;

    public float ExtentMeters => KristiansandHeightSampler.MapExtentMeters;
    public float EastMeters => KristiansandHeightSampler.EastMeters;
    public float NorthMeters => KristiansandHeightSampler.NorthMeters;
    public BoundedHeightfield Field => _field;
    public BvhStaticWorld Collision => _collision;
    public Vector3 GunBaseline { get; private set; }

    /// <summary>Legacy wire count — terrain is solid now (always 0).</summary>
    public int TerrainLineCount => 0;

    public int TerrainTriangleCount => _cachedFill.Length / 3;

    public int RoadLineCount => _cachedRoadWires.Length / 2;

    public void Rebuild()
    {
        var sw = Stopwatch.StartNew();
        InvalidateGpuMeshes();
        _extentOptions = new WorldExtentOptions
        {
            ExtentMeters = KristiansandHeightSampler.EastMeters,
            ExtentNorthMeters = KristiansandHeightSampler.NorthMeters,
            CollisionCells = CollisionCells,
            DrawCells = DisplayCells,
        };

        _sampler = new KristiansandHeightSampler();
        _field = new BoundedHeightfield(_sampler, new AxisAlignedRangeBox(EastMeters, NorthMeters));

        var mesh = HeightfieldMeshBuilder.Build(_sampler, _extentOptions);
        _collision = mesh.Collision;
        _drawVertices = mesh.DrawVertices;
        _drawCells = mesh.DrawCells;
        var afterMesh = sw.ElapsedMilliseconds;

        _cachedFill = KristiansandLandFillMesh.Build(_drawVertices, _drawCells, _drawCells);
        var afterFill = sw.ElapsedMilliseconds;

        KristiansandMapFeatures.ResetClipCache();
        KristiansandNvdbRoads.Reset();
        _cachedRoadWires = KristiansandNvdbRoads.Build(_sampler);

        GunBaseline = new Vector3(
            SimulationUnits.GunPivotX,
            _field.SampleHeight(SimulationUnits.GunPivotX, NorthMeters * 0.5f) + SimulationUnits.GunHeightOffset,
            NorthMeters * 0.5f);

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Terrain rebuild: mesh={afterMesh}ms fill={afterFill - afterMesh}ms roads={sw.ElapsedMilliseconds - afterFill}ms total={sw.ElapsedMilliseconds}ms (draw={DisplayCells} coll={CollisionCells} tris={TerrainTriangleCount} roadSeg={RoadLineCount})"));
    }

    public void PlaceGun(float x, float z)
    {
        x = Math.Clamp(x, 0f, EastMeters);
        z = Math.Clamp(z, 0f, NorthMeters);
        GunBaseline = new Vector3(x, _field.SampleHeight(x, z) + SimulationUnits.GunHeightOffset, z);
    }

    /// <summary>Uploads solid fill + roads once; redraws from GPU each frame.</summary>
    public void DrawStatic(SilkFrame frame, in Matrix4x4 viewProjection)
    {
        EnsureGpuMeshes(frame);
        if (_fillMesh is not null)
            frame.DrawWorldSolidMesh(_fillMesh, viewProjection);
        if (_roadMesh is not null)
            frame.DrawWorldLineMesh(_roadMesh, viewProjection, depthTest: false);
    }

    public void DrawDynamic(SilkWireBatch wires)
    {
        DrawRangeBoundary(wires);
    }

    public void Dispose() => InvalidateGpuMeshes();

    private void EnsureGpuMeshes(SilkFrame frame)
    {
        _fillMesh ??= _cachedFill.Length >= 3
            ? frame.CreateWorldSolidMesh(_cachedFill)
            : null;
        if (_roadMesh is null && _cachedRoadWires.Length >= 2)
            _roadMesh = frame.CreateWorldLineMesh(_cachedRoadWires);
    }

    private void InvalidateGpuMeshes()
    {
        _fillMesh?.Dispose();
        _fillMesh = null;
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
