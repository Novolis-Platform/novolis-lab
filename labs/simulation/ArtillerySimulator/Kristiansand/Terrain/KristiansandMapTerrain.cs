using Novolis.Math.Geometry;
using Novolis.Physics.Abstractions;

namespace ArtillerySimulator;

/// <summary>Shipped Kristiansand NDH + NVDB profile.</summary>
internal sealed class KristiansandMapTerrain : IMapTerrainProfile
{
    public static KristiansandMapTerrain Instance { get; } = new();

    public string Id => "kristiansand";
    public float EastMeters => KristiansandHeightSampler.EastMeters;
    public float NorthMeters => KristiansandHeightSampler.NorthMeters;
    public float MeanLatitudeDegrees => (float)KristiansandHeightSampler.MeanLatitudeDegrees;
    public float MaxElevationMeters => KristiansandHeightSampler.MaxElevationMeters;

    public IHeightSampler CreateSampler() => new KristiansandHeightSampler();

    public bool IsWater(float xMeters, float zMeters) =>
        KristiansandSurface.KindAt(xMeters, zMeters) == KristiansandSurfaceKind.Water;

    public WorldLineVertex[] BuildDecorativeRoads(IHeightSampler heights)
    {
        KristiansandMapFeatures.ResetClipCache();
        KristiansandNvdbRoads.Reset();
        return KristiansandNvdbRoads.Build(heights);
    }
}
