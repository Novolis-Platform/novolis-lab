using CalypsoCad.Generation.Fabrication;
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

        Add("fab package id", string.Equals(manifest.PackageId, CalypsoFabReleaseEmitter.PackageId, StringComparison.Ordinal));
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
            "frame profiles with holes",
            manifest.Cuts.Count(c => c.PartId.StartsWith("P-FRAME-", StringComparison.Ordinal) && c.Holes.Count > 0) >= 8,
            $"profileHoles={manifest.Cuts.Count(c => c.PartId.StartsWith("P-FRAME-", StringComparison.Ordinal) && c.Holes.Count > 0)}");
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

        var hullDevFail = manifest.Cuts.Count(c =>
            c.PartId.StartsWith("P-HULL-", StringComparison.Ordinal) && !c.DevelopabilityPass);
        Add("hull developability 100%", hullDevFail == 0, $"failed={hullDevFail}");

        var cutWithFile = manifest.Parts.Count(p =>
            p.Process == FabPartProcess.Cut
            && !string.IsNullOrEmpty(p.CutFileRelative));
        Add(
            "cut parts have dxf paths",
            cutWithFile >= manifest.Parts.Count(p => p.Process == FabPartProcess.Cut) * 0.95f,
            $"withPath={cutWithFile}");

        var bomMass = manifest.Parts.Sum(p => p.MassKg);
        var manifestMass = manifest.Totals?.TotalMassKg ?? 0f;
        if (manifestMass > 0)
        {
            var rel = MathF.Abs(bomMass - manifestMass) / manifestMass;
            Add("BOM mass vs manifest ±2%", rel <= 0.02f, $"delta={rel:P2}");
        }

        if (manifest.Totals is not null && manifest.Totals.HullSkinAreaM2 > 0)
        {
            var golden = 3796.055f;
            var rel = MathF.Abs(manifest.Totals.HullSkinAreaM2 - golden) / golden;
            Add("hull area vs manufacturer rollup", rel < 0.08f, $"delta={rel:P1}");
        }

        var outDir = fabOutputDirectory is not null
            ? Path.Combine(fabOutputDirectory, CalypsoFabReleaseEmitter.PackageId)
            : CalypsoFabReleaseEmitter.DefaultOutputDirectory;
        var manifestPath = Path.Combine(outDir, "calypso-fab-manifest.json");
        Add("fab output manifest file", File.Exists(manifestPath));
        Add("fab bom file", File.Exists(Path.Combine(outDir, "bom.json")));
        Add("fab hull seams file", File.Exists(Path.Combine(outDir, "welds", "hull-seams.json")));
        Add("fab hull weld program", File.Exists(Path.Combine(outDir, "welds", "hull-program.json")));

        Add("corridor clear width at frames", EvaluateCorridorClear(manifest, doc), "CORR_W sampled");

        return checks;
    }

    private static bool EvaluateCorridorClear(FabReleaseManifest manifest, CadDocument doc)
    {
        _ = manifest;
        var loa = Novolis.Ship.Primitives.ShipDocumentMetrics.GetLoaMeters(doc);
        return CalypsoLockGenerator.CorridorClearAtFrameSamples(loa);
    }
}
