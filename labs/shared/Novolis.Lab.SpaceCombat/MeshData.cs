using System.IO.Compression;
using System.Text.Json;
using Novolis.Simulation.SpaceCombat;

namespace Novolis.Lab.SpaceCombat;

public sealed class MeshData(float[] positions, int[] indices)
{
    public float[] Positions { get; } = positions;
    public int[] Indices { get; } = indices;
}
