using System.Text.Json;
using CalypsoCad.Session;
using Novolis.Cad.Primitives;
using Novolis.Cad.SceneBridge;
using Novolis.Math.Geometry;
using Novolis.Modeling;

namespace CalypsoCad.Export;

/// <summary>
/// Calypso-domain ship interchange meshes: structure + hull shells → OBJ / STL / FBX / GLB.
/// Format writers live in Math / Modeling; this file only picks Calypso parts and paths.
/// </summary>
internal static class ShipObjectExporter
{
    public static IReadOnlyList<string> Export(CadDocument document, string? outputDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var outDir = outputDirectory ?? CalypsoProjectPaths.MeshesDirectory;
        Directory.CreateDirectory(outDir);

        var parts = CadMeshCollector.Collect(document, IsShipObjectPart);
        if (parts.Count == 0)
            throw new InvalidOperationException("No tessellable ship structure/hull parts found.");

        var named = parts.Select(p => (p.Name, p.Mesh)).ToList();
        var merged = CadMeshCollector.Merge(parts);
        var written = new List<string>(8);

        var obj = Path.Combine(outDir, "calypso.obj");
        WavefrontObjWriter.Write(obj, named);
        written.Add(obj);

        var stl = Path.Combine(outDir, "calypso.stl");
        StlBinaryWriter.Write(stl, merged, "calypso");
        written.Add(stl);

        TryAssimp(Path.Combine(outDir, "calypso.fbx"), named, written);
        TryAssimp(Path.Combine(outDir, "calypso.glb"), named, written);

        var manifest = Path.Combine(outDir, "manifest.json");
        File.WriteAllText(
            manifest,
            JsonSerializer.Serialize(
                new
                {
                    generatedUtc = DateTime.UtcNow.ToString("o"),
                    partCount = parts.Count,
                    vertexCount = merged.VertexCount,
                    triangleCount = merged.TriangleCount,
                    files = written.Select(Path.GetFileName).ToArray(),
                    filter = "structure role + hull/oml/iml meshes",
                },
                new JsonSerializerOptions { WriteIndented = true }));
        written.Add(manifest);
        return written;
    }

    public static int Run()
    {
        CalypsoHeadlessHost.EnsureGenerated();
        var session = new CalypsoSession();
        session.RegenerateAndLoad();
        var outputs = Export(session.Document);
        Console.WriteLine(
            $"Ship objects ({outputs.Count}) → {CalypsoProjectPaths.MeshesDirectory}");
        foreach (var path in outputs)
            Console.WriteLine($"  {path}");

        var hasObj = outputs.Any(p => p.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));
        var hasStl = outputs.Any(p => p.EndsWith(".stl", StringComparison.OrdinalIgnoreCase));
        return hasObj && hasStl ? 0 : 2;
    }

    /// <summary>Structure bars/plates and manufacturer hull shells — not outfit / spaces / walls.</summary>
    internal static bool IsShipObjectPart(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var kind = entity.Kind.ToLowerInvariant();
        if (kind is "space" or "wall" or "opening" or "line" or "camera" or "light")
            return false;

        if (kind == "mesh")
            return true;

        if (HasRole(entity, "structure"))
            return true;

        var name = entity.Name ?? "";
        if (name.Contains("oml", StringComparison.OrdinalIgnoreCase)
            || name.Contains("iml", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("bh-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("girder-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("long-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("frm-", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool HasRole(CadEntity entity, string role)
    {
        if (entity.Properties is null
            || !entity.Properties.TryGetValue("role", out var el)
            || el.ValueKind != JsonValueKind.String)
            return false;
        return string.Equals(el.GetString(), role, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryAssimp(
        string path,
        IReadOnlyList<(string Name, EditableMesh Mesh)> named,
        List<string> written)
    {
        try
        {
            AssimpMeshExporter.Export(path, named);
            written.Add(path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  skip {Path.GetFileName(path)}: {ex.Message}");
        }
    }
}
