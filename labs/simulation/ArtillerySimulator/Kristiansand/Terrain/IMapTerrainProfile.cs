using Novolis.Math.Geometry;
using Novolis.Physics.Abstractions;

namespace ArtillerySimulator;

/// <summary>One map's extents, height, water, and decorative (non-gameplay) road wires.</summary>
internal interface IMapTerrainProfile
{
    string Id { get; }
    float EastMeters { get; }
    float NorthMeters { get; }
    float MeanLatitudeDegrees { get; }
    float MaxElevationMeters { get; }

    IHeightSampler CreateSampler();
    bool IsWater(float xMeters, float zMeters);
    WorldLineVertex[] BuildDecorativeRoads(IHeightSampler heights);
}
