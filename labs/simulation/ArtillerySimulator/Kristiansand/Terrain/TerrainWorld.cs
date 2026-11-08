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

/// <summary>Shared heightfield host — rebuilt from an <see cref="IMapTerrainProfile"/>.</summary>
internal sealed class TerrainWorld : IDisposable
{
    private static readonly Color BoundaryColor = Color.FromArgb(255, 200, 180, 90);

    public const int DisplayCells = 512;
    public const int CollisionCells = 192;

    private WorldExtentOptions _extentOptions = new()
    {
        ExtentMeters = KristiansandHeightSampler.EastMeters,
        ExtentNorthMeters = KristiansandHeightSampler.NorthMeters,
        CollisionCells = CollisionCells,
        DrawCells = DisplayCells,
    };

    private IMapTerrainProfile _profile = KristiansandMapTerrain.Instance;
    private BoundedHeightfield _field = null!;
    private IHeightSampler _sampler = null!;
    private BvhStaticWorld _collision = null!;
    private Vector3[] _drawVertices = [];
    private WorldSolidVertex[] _cachedFill = [];
    private WorldLineVertex[] _cachedRoadWires = [];
    private WorldSolidGpuMesh? _fillMesh;
    private WorldLineGpuMesh? _roadMesh;
    private int _drawCells;
    private float _eastMeters = KristiansandHeightSampler.EastMeters;
    private float _northMeters = KristiansandHeightSampler.NorthMeters;

    public IMapTerrainProfile Profile => _profile;
    public float ExtentMeters => MathF.Max(_eastMeters, _northMeters);
    public float EastMeters => _eastMeters;
    public float NorthMeters => _northMeters;
    public BoundedHeightfield Field => _field;
    public BvhStaticWorld Collision => _collision;
    public Vector3 GunBaseline { get; private set; }

    public int TerrainLineCount => 0;
    public int TerrainTriangleCount => _cachedFill.Length / 3;
    public int RoadLineCount => _cachedRoadWires.Length / 2;

    public void Rebuild() => Rebuild(KristiansandMapTerrain.Instance);

    public void Rebuild(IMapTerrainProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var sw = Stopwatch.StartNew();
        InvalidateGpuMeshes();
        _profile = profile;
        _eastMeters = profile.EastMeters;
        _northMeters = profile.NorthMeters;
        _extentOptions = new WorldExtentOptions
        {
            ExtentMeters = _eastMeters,
            ExtentNorthMeters = _northMeters,
            CollisionCells = CollisionCells,
            DrawCells = DisplayCells,
        };

        _sampler = profile.CreateSampler();
        _field = new BoundedHeightfield(_sampler, new AxisAlignedRangeBox(_eastMeters, _northMeters));

        var mesh = HeightfieldMeshBuilder.Build(_sampler, _extentOptions);
        _collision = mesh.Collision;
        _drawVertices = mesh.DrawVertices;
        _drawCells = mesh.DrawCells;
        var afterMesh = sw.ElapsedMilliseconds;

        _cachedFill = KristiansandLandFillMesh.Build(
            _drawVertices,
            _drawCells,
            _drawCells,
            profile.IsWater);
        var afterFill = sw.ElapsedMilliseconds;

        _cachedRoadWires = profile.BuildDecorativeRoads(_sampler);

        var pivotY = _field.SampleHeight(SimulationUnits.GunPivotX, _northMeters * 0.5f);
        GunBaseline = new Vector3(SimulationUnits.GunPivotX, pivotY, _northMeters * 0.5f);

        var frameNote = string.Equals(profile.Id, "oslofjorden", StringComparison.OrdinalIgnoreCase)
            ? string.Create(
                CultureInfo.InvariantCulture,
                $" east={_eastMeters:F0}m north={_northMeters:F0}m oscarsborgT={OslofjordenGeodesy.OscarsborgNorthFraction:F2}")
            : string.Empty;
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Terrain rebuild [{profile.Id}]: mesh={afterMesh}ms fill={afterFill - afterMesh}ms roads={sw.ElapsedMilliseconds - afterFill}ms total={sw.ElapsedMilliseconds}ms (draw={DisplayCells} coll={CollisionCells} tris={TerrainTriangleCount} roadSeg={RoadLineCount}){frameNote}"));
    }

    public void PlaceGun(float x, float z, float gunHeightOffsetMeters = 0f)
    {
        x = Math.Clamp(x, 0f, _eastMeters);
        z = Math.Clamp(z, 0f, _northMeters);
        GunBaseline = new Vector3(x, _field.SampleHeight(x, z) + gunHeightOffsetMeters, z);
    }

    public void DrawStatic(SilkFrame frame, in Matrix4x4 viewProjection)
    {
        EnsureGpuMeshes(frame);
        if (_fillMesh is not null)
            frame.DrawWorldSolidMesh(_fillMesh, viewProjection);
        if (_roadMesh is not null)
            frame.DrawWorldLineMesh(_roadMesh, viewProjection, depthTest: false);
    }

    public void DrawDynamic(SilkWireBatch wires) => DrawRangeBoundary(wires);

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
