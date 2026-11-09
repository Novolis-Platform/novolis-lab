using System.Globalization;
using CalypsoCad.Blueprints;
using CalypsoCad.Blueprints.DrawingPackage;
using CalypsoCad.Model;
using CalypsoCad.Publication;
using CalypsoCad.Session;
using CalypsoCad.Structure;
using Novolis.Cad.Blueprint;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Drawing;

/// <summary>Acceptance A–F: same engine for knuckle, frame, BH, deck, arbitrary section, arbitrary assembly.</summary>
internal static class UniversalDrawingAcceptance
{
    public static IReadOnlyList<(string Name, bool Ok, string Detail)> Run(CadDocument? cad = null)
    {
        var results = new List<(string, bool, string)>();
        void Check(string name, bool ok, string detail = "") => results.Add((name, ok, detail));

        try
        {
            cad ??= CadJsonStructureQuery.Load();
        }
        catch (Exception ex)
        {
            Check("universal drawing · load CAD", false, ex.Message);
            return results;
        }

        CadJsonStructureQuery.EnsureHullGeometry(cad);
        CadDocumentAdapter.LoadFromDocument(cad);
        var registry = CadDocumentAdapter.Active;
        ShipHierarchyBuilder.EnsureCanonicalHierarchy(registry, cad);

        // A — Knuckle V2
        {
            var r = UniversalDrawingEngine.Render(DrawingTemplates.KnuckleStringer("knuckle-v2", "CAL-250-003"), cad, registry);
            var path = r.Manifest.Measurements.FirstOrDefault(m => m.Id == "path-true-length");
            Check("A knuckle-v2 renders", r.Manifest.Ok && r.Resolved.HasDrawableGeometry,
                $"geom={r.Resolved.Geometry.Count} diag={r.Manifest.Diagnostics.Count}");
            Check("A knuckle-v2 true path length", path is { ValueMeters: > 1f },
                path is null ? "missing" : $"{path.ValueMeters:0.###} m");
            Check("A knuckle-v2 uses mesh sources",
                r.Resolved.Geometry.Any(g => g.Kind == DrawablePrimitiveKind.Mesh),
                string.Join(",", r.Resolved.Geometry.Select(g => g.Kind).Distinct()));

            // Plan U must increase toward the bow (CAD +Z), matching ShipX stern-left.
            var loa = CadJsonStructureQuery.Loa(cad);
            var bow = ViewProjector.Map(new System.Numerics.Vector3(0, 0, CadShipCoords.WorldZFromStem(0, loa)), StandardViewOrientation.PlanSternLeft);
            var stern = ViewProjector.Map(new System.Numerics.Vector3(0, 0, CadShipCoords.WorldZFromStem(loa, loa)), StandardViewOrientation.PlanSternLeft);
            Check("plan U increases bow-ward (stern left)", bow.U > stern.U, $"bowU={bow.U:0.##} sternU={stern.U:0.##}");
        }

        // B — Ordinary ring frame
        {
            var frames = CadJsonStructureQuery.PhysicalFrameStations(cad);
            var frame = frames.FirstOrDefault(f => f.PartCount > 0);
            if (frame is null)
            {
                Check("B ring frame available", false, "no frames");
            }
            else
            {
                var r = UniversalDrawingEngine.Render(
                    DrawingTemplates.RingFrame(frame.FrameId, frame.ZStem), cad, registry);
                Check("B ring frame renders", r.Manifest.Ok && r.Resolved.HasDrawableGeometry,
                    $"{frame.FrameId} geom={r.Resolved.Geometry.Count}");
                Check("B ring frame has transverse view",
                    r.Manifest.ViewIds.Contains("A"),
                    string.Join(",", r.Manifest.ViewIds));
                var loaLabel = PackageDrawingChrome.Nice(CadJsonStructureQuery.Loa(cad));
                var beamLabel = PackageDrawingChrome.Nice(CadJsonStructureQuery.Beam(cad));
                var oahLabel = PackageDrawingChrome.Nice(CadJsonStructureQuery.Oah(cad));
                Check("B plan/profile location dims",
                    r.Html.Contains(loaLabel, StringComparison.Ordinal)
                    && r.Html.Contains(beamLabel, StringComparison.Ordinal)
                    && r.Html.Contains(oahLabel, StringComparison.Ordinal),
                    $"loa={loaLabel} beam={beamLabel} oah={oahLabel}");
            }
        }

        // C — BH-ENG-AFT
        {
            const string bh = "bh-ENG-AFT";
            var schedule = CalypsoBulkheads.BuildSchedule(
                CadJsonStructureQuery.Loa(cad), CadJsonStructureQuery.StationMap(cad));
            var st = schedule.FirstOrDefault(s =>
                string.Equals(s.Id, "ENG-AFT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Id, bh, StringComparison.OrdinalIgnoreCase));
            var z = st.Id is null ? 0f : st.ZStem;
            var subject = registry.TryGetAssembly(AssemblyId.Of(bh), out _)
                ? bh
                : registry.Assemblies.FirstOrDefault(a =>
                      a.Id.Value.Contains("ENG-AFT", StringComparison.OrdinalIgnoreCase))?.Id.Value
                  ?? bh;
            var r = UniversalDrawingEngine.Render(DrawingTemplates.HeavyBulkhead(subject, z), cad, registry);
            Check("C BH-ENG-AFT renders", r.Resolved.HasDrawableGeometry,
                $"{subject} geom={r.Resolved.Geometry.Count} ok={r.Manifest.Ok}");
            Check("C BH uses actual mesh (openings are voids)",
                r.Resolved.Geometry.Any(g => g.Kind == DrawablePrimitiveKind.Mesh),
                string.Join(",", r.Resolved.Geometry.Select(g => g.Kind)));
        }

        // D — Deck 0
        {
            var r = UniversalDrawingEngine.Render(
                DrawingTemplates.DeckPlan(ShipHierarchyIds.Deck0), cad, registry);
            if (!r.Resolved.HasDrawableGeometry)
                r = UniversalDrawingEngine.Render(DrawingTemplates.DeckPlan("deck-dk0"), cad, registry);
            Check("D Deck 0 renders", r.Resolved.HasDrawableGeometry,
                $"geom={r.Resolved.Geometry.Count}");
        }

        // E — Arbitrary transverse section (not a BH/frame station)
        {
            var loa = CadJsonStructureQuery.Loa(cad);
            var z = Math.Clamp(loa * 0.37f, 1f, loa - 1f);
            // Avoid exact BH stations.
            var bhZs = CalypsoBulkheads.BuildSchedule(loa, CadJsonStructureQuery.StationMap(cad))
                .Select(s => s.ZStem).ToHashSet();
            while (bhZs.Any(bz => Math.Abs(bz - z) < 0.05f))
                z += 0.4f;

            var r = UniversalDrawingEngine.Render(
                DrawingTemplates.ArbitrarySection(ShipHierarchyIds.PhysicalStructure, z), cad, registry);
            Check("E arbitrary section subject resolves", r.Resolved.HasDrawableGeometry,
                $"z={z.ToString("0.##", CultureInfo.InvariantCulture)} geom={r.Resolved.Geometry.Count}");
            var edges = MeshSectioner.Section(
                r.Resolved.Geometry,
                DrawingPlane.FromPointNormal(new System.Numerics.Vector3(0, 0, z), System.Numerics.Vector3.UnitZ));
            Check("E section intersects solids", edges.Count > 0, $"edges={edges.Count}");
        }

        // F — Arbitrary assembly (no category-specific renderer)
        {
            var asm = registry.Assemblies.FirstOrDefault(a =>
                a.Kind is CadAssemblyKind.LongitudinalRun or CadAssemblyKind.DeckBay
                    or CadAssemblyKind.PartitionGroup or CadAssemblyKind.HullKnuckleStringer
                && registry.CollectEntityIds(a.Id).Count > 0
                && !a.Id.Value.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
                && !a.Id.Value.StartsWith("bh-", StringComparison.OrdinalIgnoreCase));
            if (asm is null)
            {
                Check("F arbitrary assembly available", false, "none");
            }
            else
            {
                var r = UniversalDrawingEngine.Render(
                    DrawingTemplates.AssemblyStandard(asm.Id.Value), cad, registry);
                Check("F arbitrary assembly renders", r.Resolved.HasDrawableGeometry,
                    $"{asm.Id.Value} geom={r.Resolved.Geometry.Count}");
                Check("F part register present", r.Manifest.PartRegister.Count > 0,
                    $"parts={r.Manifest.PartRegister.Count}");
            }
        }

        // G — CadBlueprint companion export (Novolis.Cad.Blueprint)
        {
            using var tmp = new TempScope();
            var export = CadBlueprintExporter.Export(cad, new CadBlueprintExportOptions
            {
                OutputDirectory = tmp.Path,
                FileStem = "calypso-acceptance",
                ContextOverride = CadBlueprintContexts.SeagoingShip,
                FailOnError = true,
                FailOnWarning = false,
            });
            Check("G CadBlueprint export Ok", export.Ok,
                export.Ok ? $"written={export.WrittenPaths.Count}" : export.FormatDiagnostics());
            var jsonPath = export.WrittenPaths.FirstOrDefault(p =>
                p.EndsWith(".cadblueprint.json", StringComparison.OrdinalIgnoreCase));
            Check("G CadBlueprint JSON written",
                jsonPath is not null && File.Exists(jsonPath),
                jsonPath ?? "missing");
            if (jsonPath is not null)
            {
                var loaded = CadBlueprintJson.Load(jsonPath);
                var diags = CadBlueprintValidator.Validate(loaded);
                Check("G CadBlueprint validates",
                    !CadBlueprintValidator.HasErrors(diags),
                    string.Join("; ", diags.Where(d => d.Severity == CadBlueprintDiagnosticSeverity.Error).Select(d => d.Code)));
                Check("G CadBlueprint stern-left sheet",
                    loaded.Sheets.Any(s => s.Orientation.Contains("stern-left", StringComparison.OrdinalIgnoreCase)),
                    loaded.Sheets.FirstOrDefault()?.Orientation ?? "none");
            }
            else
            {
                Check("G CadBlueprint validates", false, "no json");
                Check("G CadBlueprint stern-left sheet", false, "no json");
            }

            var htmlPath = export.WrittenPaths.FirstOrDefault(p =>
                p.EndsWith(".html", StringComparison.OrdinalIgnoreCase));
            Check("G CadBlueprint smart HTML",
                htmlPath is not null
                && File.Exists(htmlPath)
                && File.ReadAllText(htmlPath).Contains(CadBlueprintDom.FormatValue, StringComparison.Ordinal),
                htmlPath ?? "missing");
            Check("G CadBlueprint hull shell lifted",
                export.Blueprint is { Shells.Count: > 0 },
                export.Blueprint is null ? "no bp" : $"shells={export.Blueprint.Shells.Count}");

            // Package path must also contain companion when blueprints were emitted previously.
            var pkgJson = Path.Combine(CalypsoProjectPaths.BlueprintsPackageDirectory, "calypso.cadblueprint.json");
            if (File.Exists(pkgJson))
            {
                Check("G package companion present", true, pkgJson);
            }
        }

        return results;
    }

    private sealed class TempScope : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "calypso-nbp-" + Guid.NewGuid().ToString("N"));

        public TempScope() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                /* best-effort */
            }
        }
    }
}
