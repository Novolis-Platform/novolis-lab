using Novolis.Physics.Abstractions;

namespace ArtillerySimulator.Game;

internal sealed class AppHeightSampler(int seed, bool flat, TerrainStyle style) : IHeightSampler
{
    public float SampleHeight(float x, float z) =>
        flat ? 0f : TerrainHeightSampler.Sample(x, z, seed, style);
}
