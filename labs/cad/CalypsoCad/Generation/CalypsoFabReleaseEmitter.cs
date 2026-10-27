using System.Text;
using System.Text.Json;
using CalypsoCad.Generation.Fabrication;
using CalypsoCad.Models;
using Novolis.Cad.Primitives;
using Novolis.Ship.Primitives;
using Novolis.Ship.Structure;

namespace CalypsoCad.Generation;

/// <summary>CAL-MFG-002 automated fabrication release (full vessel).</summary>
public static class CalypsoFabReleaseEmitter
{
    public const string PackageId = "CAL-MFG-002";

    public static string DefaultOutputDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "CalypsoCad",
            "generated",
            PackageId);

    public static string Emit(string? outputDirectory = null, string? generatedCadDirectory = null)
    {
        var genDir = generatedCadDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "CalypsoCad",
            "generated");
        var outDir = outputDirectory ?? Path.Combine(genDir, PackageId);
        Directory.CreateDirectory(outDir);

        CalypsoLockGenerator.Generate(genDir);
        var cadPath = Path.Combine(genDir, "calypso.cadjson");
        var cad = JsonSerializer.Deserialize<CadDocument>(File.ReadAllText(cadPath), CadJson.Options)
                  ?? throw new InvalidOperationException("Missing calypso.cadjson");

        var loa = ShipDocumentMetrics.GetLoaMeters(cad);
        var hullPath = ResolveHullJson();
        var hull = ManufacturerHullDocument.Load(hullPath);

        var parts = new List<FabPart>();
        var cuts = new List<FabCutProfile>();
        var nests = new List<FabNestPlacement>();
        var welds = new List<FabWeldSeam>();
        var bomLines = new List<PlateBomLine>();
        var sequence = new List<FabAssemblyInstance>();

        HullFabExporter.Export(hull, outDir, parts, cuts, nests, welds, bomLines);

        var seq = 0;
        foreach (var p in parts.Where(p => p.Process == FabPartProcess.Cut && p.PartId.StartsWith("P-HULL-", StringComparison.Ordinal)))
        {
            sequence.Add(new FabAssemblyInstance
            {
                PartId = p.PartId,
                SequenceIndex = seq++,
                ParentAssembly = "ASM-HULL-SKIN",
            });
        }

        CadFabPartCollector.CollectFromDocument(cad, loa, outDir, parts, cuts, sequence, bomLines, ref seq);

        var hullArea = parts.Where(p => p.PartId.StartsWith("P-HULL-", StringComparison.Ordinal))
            .Sum(p => cuts.FirstOrDefault(c => c.PartId == p.PartId)?.AreaM2 ?? 0f);
        var manifest = new FabReleaseManifest
        {
            PackageId = PackageId,
            Revision = "002",
            GeneratedAtUtc = DateTime.UtcNow.ToString("o"),
            Parts = parts,
            Cuts = cuts,
            Nests = nests,
            Welds = welds,
            Sequence = sequence.OrderBy(s => s.SequenceIndex).ToList(),
            Totals = new FabReleaseTotals
            {
                PartCount = parts.Count,
                CutPartCount = parts.Count(p => p.Process == FabPartProcess.Cut),
                InstallPartCount = parts.Count(p => p.Process == FabPartProcess.Install),
                SheetCount = nests.Select(n => n.SheetId).Distinct().Count(),
                TotalMassKg = parts.Sum(p => p.MassKg),
                HullSkinAreaM2 = hullArea,
            },
        };

        var weldsPath = Path.Combine(outDir, "welds", "hull-seams.json");
        Directory.CreateDirectory(Path.GetDirectoryName(weldsPath)!);
        File.WriteAllText(weldsPath, JsonSerializer.Serialize(welds, FabricationJson.Options));

        var manifestPath = Path.Combine(outDir, "calypso-fab-manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, FabricationJson.Options));

        var bom = new ShipBom
        {
            Drawing = PackageId,
            Rev = "002",
            Lines = bomLines,
        };
        var bomPath = Path.Combine(outDir, "bom.json");
        File.WriteAllText(bomPath, JsonSerializer.Serialize(bom, FabricationJson.Options));

        manifest.ContentHashes["calypso-fab-manifest.json"] = HullFabExporter.Sha256File(manifestPath);
        manifest.ContentHashes["bom.json"] = HullFabExporter.Sha256File(bomPath);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, FabricationJson.Options));

        ShipFabReleaseDocument.Attach(cad, manifest);
        File.WriteAllText(cadPath, JsonSerializer.Serialize(cad, CadJson.Options));

        var htmlPath = Path.Combine(outDir, $"{PackageId}.html");
        WriteHtml(htmlPath, manifest, hull);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, FabricationJson.Options));

        CopyDocPointer(outDir);
        return htmlPath;
    }

    private static void CopyDocPointer(string outDir)
    {
        var docsDir = Path.Combine(
            AppContext.BaseDirectory,
            "docs",
            "internals");
        if (!Directory.Exists(docsDir))
        {
            docsDir = @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\internals";
        }

        if (!Directory.Exists(docsDir))
            return;
        var pointer = Path.Combine(docsDir, $"{PackageId}.json");
        File.WriteAllText(
            pointer,
            JsonSerializer.Serialize(new
            {
                packageId = PackageId,
                note = "Full release emitted to LocalAppData; regenerate with --fab-release. STEP/GD&T: CAL-MFG-003.",
                output = outDir,
            }, FabricationJson.Options));
    }

    private static void WriteHtml(string path, FabReleaseManifest manifest, ManufacturerHullDocument hull)
    {
        var sb = new StringBuilder();
        sb.Append($"<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"/><title>{PackageId}</title></head><body>");
        sb.Append($"<h1>{PackageId} — automated fabrication release</h1>");
        sb.Append("<p>Flat-plate pepakura hull (AISI 316L, t=8 mm) plus profile-based structure cuts, partitions, and outfit install kit.</p>");
        sb.Append("<h2>Totals</h2><table><tr><th>Item</th><th>Value</th></tr>");
        sb.Append($"<tr><td>Parts</td><td>{manifest.Totals?.PartCount}</td></tr>");
        sb.Append($"<tr><td>Cut parts</td><td>{manifest.Totals?.CutPartCount}</td></tr>");
        sb.Append($"<tr><td>Install parts</td><td>{manifest.Totals?.InstallPartCount}</td></tr>");
        sb.Append($"<tr><td>Nest sheets</td><td>{manifest.Totals?.SheetCount}</td></tr>");
        sb.Append($"<tr><td>Total mass kg</td><td>{manifest.Totals?.TotalMassKg:F1}</td></tr>");
        sb.Append($"<tr><td>Hull skin area m²</td><td>{manifest.Totals?.HullSkinAreaM2:F2}</td></tr>");
        sb.Append($"<tr><td>Golden hull area m²</td><td>{hull.GoldenSkinAreaM2:F2}</td></tr>");
        sb.Append("</table>");
        sb.Append("<h2>Out of scope (CAL-MFG-003)</h2><ul><li>STEP AP242</li><li>GD&amp;T / flight allowables</li><li>Fastener torque sheets</li></ul>");
        sb.Append("</body></html>");
        File.WriteAllText(path, sb.ToString());
    }

    private static string ResolveHullJson()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "lock", "CAL-HULL-CAD-001.json"),
            @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\manufacturer\CAL-HULL-CAD-001.json",
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        throw new FileNotFoundException("CAL-HULL-CAD-001.json not found");
    }
}
