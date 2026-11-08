using System.Text.Json;

namespace CalypsoCad.Structure;

/// <summary>Emit-time hull-contour deviation for ordinary annular frames.</summary>
internal static class FrameContourEmitLog
{
    public sealed record Entry(
        string PartId,
        float StationZStem,
        float StructuralInsetM,
        float MaxDeviationMm,
        int TriangleCount,
        int VertexCount);

    private static readonly List<Entry> Entries = [];

    public static void Reset() => Entries.Clear();

    public static void Record(
        string partId,
        float zStem,
        float insetM,
        float maxDeviationMm,
        int tris,
        int verts) =>
        Entries.Add(new Entry(partId, zStem, insetM, maxDeviationMm, tris, verts));

    public static IReadOnlyList<Entry> Snapshot() => Entries.ToList();

    public static string WriteJson(string path, float toleranceMm = 25f)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var failures = Entries.Count(e => e.MaxDeviationMm > toleranceMm);
        var payload = new
        {
            frameAnnulusCount = Entries.Count,
            failures,
            toleranceMm,
            note = "Deviation is emit-time MaxRingDeviation vs HullSectionAuthority.InnerRing (geometric inset). Reconstructing from mesh verts is not used.",
            frames = Entries.Select(e => new
            {
                partId = e.PartId,
                stationZStem = e.StationZStem,
                structuralInsetM = e.StructuralInsetM,
                maxDeviationMm = e.MaxDeviationMm,
                toleranceMm,
                passed = e.MaxDeviationMm <= toleranceMm,
                triangleCount = e.TriangleCount,
                vertexCount = e.VertexCount,
            }),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }
}
