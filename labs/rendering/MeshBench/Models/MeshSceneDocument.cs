namespace MeshBench.Models;

internal sealed class MeshSceneDocument
{
    public List<MeshPartRecord> Parts { get; set; } = [];

    public OrbitCameraState Camera { get; set; } = new();
}

internal sealed class OrbitCameraState
{
    public float Yaw { get; set; } = 0.9f;

    public float Pitch { get; set; } = 0.35f;

    public float Distance { get; set; } = 4.5f;

    public float[] Target { get; set; } = [0f, 0.45f, 0f];
}
