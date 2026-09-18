using System.Text.Json;
using CalypsoCad.Generation;
using CalypsoCad.Models;
using CalypsoInternalsCad.Export;
using Novolis.Cad.Primitives;

namespace CalypsoInternalsCad.Pipeline;

/// <summary>
/// CAL-INT drawings (lock JSON + manufacturer hull) → Novolis CAD companions + Wavefront OBJ.
/// </summary>
internal static class InternalsCadPipeline
{
    public static string DefaultOutputDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "CalypsoInternalsCad",
            "generated");

    public static PipelineResult Run(string? outputDirectory = null)
    {
        var dir = outputDirectory ?? DefaultOutputDirectory;
        Directory.CreateDirectory(dir);

        var generated = CalypsoLockGenerator.Generate(dir);
        var cadPath = Path.Combine(generated, "calypso.cadjson");
        var cad = JsonSerializer.Deserialize<CadDocument>(File.ReadAllText(cadPath), CadJson.Options)
                  ?? throw new InvalidOperationException("Failed to read generated calypso.cadjson");

        // Stable names for this dogfood app (keep calypso.* for ShipDesigner import parity).
        File.Copy(cadPath, Path.Combine(generated, "calypso-internals.cadjson"), overwrite: true);
        File.Copy(
            Path.Combine(generated, "calypso.cadlayers.json"),
            Path.Combine(generated, "calypso-internals.cadlayers.json"),
            overwrite: true);
        File.Copy(
            Path.Combine(generated, "calypso.cadshapejson"),
            Path.Combine(generated, "calypso-internals.cadshapejson"),
            overwrite: true);

        var objPath = Path.Combine(generated, "calypso-internals.obj");
        var mtlPath = Path.Combine(generated, "calypso-internals.mtl");
        var meshStats = WavefrontObjExporter.Write(cad, objPath, mtlPath);

        var manifest = new
        {
            drawing = "CAL-INT-GA-001",
            source = "CalypsoCad docs/internals + docs/manufacturer",
            generatedAt = DateTime.UtcNow.ToString("o"),
            outputDirectory = generated,
            cad = new
            {
                entities = cad.Entities.Count,
                spaces = cad.Entities.Count(e => string.Equals(e.Kind, "space", StringComparison.OrdinalIgnoreCase)),
                walls = cad.Entities.Count(e => string.Equals(e.Kind, "wall", StringComparison.OrdinalIgnoreCase)),
                openings = cad.Entities.Count(e => string.Equals(e.Kind, "opening", StringComparison.OrdinalIgnoreCase)),
                meshes = cad.Entities.Count(e => string.Equals(e.Kind, "mesh", StringComparison.OrdinalIgnoreCase)),
            },
            obj = meshStats,
            files = new[]
            {
                "calypso.cadjson",
                "calypso.cadlayers.json",
                "calypso.cadshapejson",
                "calypso-internals.cadjson",
                "calypso-internals.cadlayers.json",
                "calypso-internals.cadshapejson",
                "calypso-internals.obj",
                "calypso-internals.mtl",
                "manifest.json",
            },
        };
        File.WriteAllText(
            Path.Combine(generated, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        return new PipelineResult(generated, cad, meshStats);
    }
}

internal sealed record PipelineResult(
    string Directory,
    CadDocument Cad,
    WavefrontObjExporter.MeshExportStats Obj);
