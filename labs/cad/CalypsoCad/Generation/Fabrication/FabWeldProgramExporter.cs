using System.Numerics;
using System.Text.Json;
using Novolis.Ship.Structure;

namespace CalypsoCad.Generation.Fabrication;

internal static class FabWeldProgramExporter
{
    internal sealed record WeldStep(
        string SeamId,
        string FaceA,
        string FaceB,
        int WeldOrder,
        string Process,
        string Filler,
        float MidX,
        float MidY,
        float MidZ,
        float NormalX,
        float NormalY,
        float NormalZ);

    public static void Write(string path, ManufacturerHullDocument hull, List<FabWeldSeam> seams)
    {
        var faceCentroids = BuildFaceCentroids(hull);
        var steps = new List<WeldStep>();
        var order = 0;
        foreach (var seam in seams)
        {
            var mid = FaceMidpoint(faceCentroids, seam.FaceA, seam.FaceB);
            steps.Add(new WeldStep(
                seam.SeamId,
                seam.FaceA,
                seam.FaceB,
                order++,
                seam.Process,
                seam.Filler,
                mid.X,
                mid.Y,
                mid.Z,
                mid.Nx,
                mid.Ny,
                mid.Nz));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(steps, FabricationJson.Options));
    }

    private static Dictionary<string, Vector3> BuildFaceCentroids(ManufacturerHullDocument hull)
    {
        var map = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        foreach (var face in hull.OmlFaces)
        {
            if (face.VertIds.Length < 4)
                continue;
            if (!TryCentroid(hull.Points, face.VertIds, out var c))
                continue;
            map[face.Id] = c;
        }

        return map;
    }

    private static bool TryCentroid(
        Dictionary<string, Vector3> pts,
        string[] ids,
        out Vector3 centroid)
    {
        centroid = default;
        var sum = Vector3.Zero;
        var n = 0;
        foreach (var id in ids)
        {
            if (!pts.TryGetValue(id, out var p))
                continue;
            sum += p;
            n++;
        }

        if (n == 0)
            return false;
        centroid = sum / n;
        return true;
    }

    private static (float X, float Y, float Z, float Nx, float Ny, float Nz) FaceMidpoint(
        Dictionary<string, Vector3> centroids,
        string faceA,
        string faceB)
    {
        if (centroids.TryGetValue(faceA, out var a) && centroids.TryGetValue(faceB, out var b))
        {
            var mid = (a + b) * 0.5f;
            var n = Vector3.Normalize(b - a);
            if (n.LengthSquared() < 1e-6f)
                n = Vector3.UnitZ;
            return (mid.X, mid.Y, mid.Z, n.X, n.Y, n.Z);
        }

        return (0f, 0f, 0f, 0f, 0f, 1f);
    }
}
