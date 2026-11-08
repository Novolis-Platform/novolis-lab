using Novolis.Physics.Abstractions;

namespace ArtillerySimulator;

internal sealed class OslofjordenHeightSampler : IHeightSampler
{
    public float SampleHeight(float x, float z) => OslofjordenSurface.SampleDrawHeight(x, z);
}
