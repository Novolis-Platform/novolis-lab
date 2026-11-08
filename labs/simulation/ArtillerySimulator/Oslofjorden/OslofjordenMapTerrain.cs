using Novolis.Math.Geometry;
using Novolis.Physics.Abstractions;

namespace ArtillerySimulator;

internal sealed class OslofjordenMapTerrain : IMapTerrainProfile
{
    public static OslofjordenMapTerrain Instance { get; } = new();

    public string Id => "oslofjorden";
    public float EastMeters => OslofjordenGeodesy.EastMeters;
    public float NorthMeters => OslofjordenGeodesy.NorthMeters;
    public float MeanLatitudeDegrees => (float)OslofjordenGeodesy.MeanLatitudeDegrees;
    public float MaxElevationMeters => OslofjordenHeightSampler.MaxElevationMeters;

    public IHeightSampler CreateSampler() => new OslofjordenHeightSampler();

    public bool IsWater(float xMeters, float zMeters) => OslofjordenSurface.IsWater(xMeters, zMeters);

    public WorldLineVertex[] BuildDecorativeRoads(IHeightSampler heights) =>
        OslofjordenDecorativeRoads.Build(heights);
}
