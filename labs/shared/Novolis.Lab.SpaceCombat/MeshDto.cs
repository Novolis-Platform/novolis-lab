using System.IO.Compression;
using System.Text.Json;
using Novolis.Simulation.SpaceCombat;

namespace Novolis.Lab.SpaceCombat;

internal sealed class MeshDto
{
    public string SourceKey { get; set; } = "";
    public float[] Positions { get; set; } = [];
    public int[] Indices { get; set; } = [];
}
