using Novolis.Cad.Primitives;
using Novolis.Ship.Structure;

namespace CalypsoCad.Generation;

internal static class CalypsoFabReleaseChecks
{
    internal readonly record struct Check(string Name, bool Ok, string Detail);

    public static List<Check> Evaluate(CadDocument doc, string? fabOutputDirectory = null)
    {
        var checks = new List<Check>();
        void Add(string name, bool ok, string detail = "") => checks.Add(new Check(name, ok, detail));

        Add(
            "fab manifest on document",
            ShipFabReleaseDocument.TryGetManifest(doc, out var manifest) && manifest is not null);
        if (!ShipFabReleaseDocument.TryGetManifest(doc, out manifest) || manifest is null)
            return checks;

        Add("fab package id", string.Equals(manifest.PackageId, "CAL-MFG-001", StringComparison.Ordinal));
        Add("fab parts registered", manifest.Parts.Count >= 80, $"parts={manifest.Parts.Count}");
        Add(
            "hull OML cut parts",
            manifest.Parts.Count(p => p.PartId.StartsWith("P-HULL-", StringComparison.Ordinal)) >= 40,
            $"hull={manifest.Parts.Count(p => p.PartId.StartsWith("P-HULL-", StringComparison.Ordinal))}");
        Add(
            "structure cut parts",
            manifest.Parts.Count(p => p.PartId.StartsWith("P-FRAME-", StringComparison.Ordinal)) >= 10,
            $"frames={manifest.Parts.Count(p => p.PartId.StartsWith("P-FRAME-", StringComparison.Ordinal))}");
        Add(
            "outfit install parts",
            manifest.Parts.Count(p => p.PartId.StartsWith("P-OUTFIT-", StringComparison.Ordinal)) >= 12,
            $"outfit={manifest.Parts.Count(p => p.PartId.StartsWith("P-OUTFIT-", StringComparison.Ordinal))}");
        Add(
            "assembly sequence",
            manifest.Sequence.Count >= manifest.Parts.Count / 2,
            $"seq={manifest.Sequence.Count}");
        Add(
            "nest sheets",
            manifest.Totals?.SheetCount is > 0 && manifest.Nests.Count > 0,
            $"sheets={manifest.Totals?.SheetCount} placements={manifest.Nests.Count}");
        Add(
            "hull weld seams",
            manifest.Welds.Count >= 30,
            $"welds={manifest.Welds.Count}");

        var cutWithFile = manifest.Parts.Count(p =>
            p.Process == FabPartProcess.Cut
            && !string.IsNullOrEmpty(p.CutFileRelative));
        Add(
            "cut parts have dxf paths",
            cutWithFile >= manifest.Parts.Count(p => p.Process == FabPartProcess.Cut) * 0.95f,
            $"withPath={cutWithFile}");

        if (manifest.Totals is not null && manifest.Totals.HullSkinAreaM2 > 0 && manifest.Totals.HullSkinAreaM2 > 0)
        {
            var golden = 3796.055f;
            var rel = MathF.Abs(manifest.Totals.HullSkinAreaM2 - golden) / golden;
            Add("hull area vs manufacturer rollup", rel < 0.08f, $"delta={rel:P1}");
        }

        var outDir = fabOutputDirectory is not null
            ? Path.Combine(fabOutputDirectory, "CAL-MFG-001")
            : CalypsoFabReleaseEmitter.DefaultOutputDirectory;
        var manifestPath = Path.Combine(outDir, "calypso-fab-manifest.json");
        Add("fab output manifest file", File.Exists(manifestPath));
        Add("fab bom file", File.Exists(Path.Combine(outDir, "bom.json")));
        Add("fab hull seams file", File.Exists(Path.Combine(outDir, "welds", "hull-seams.json")));

        return checks;
    }
}
