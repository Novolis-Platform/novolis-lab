namespace MeshBench.Models;

internal sealed class MeshSceneDocument
{
    public List<MeshPartRecord> Parts { get; set; } = [];

    public OrbitCameraState Camera { get; set; } = new();
}
