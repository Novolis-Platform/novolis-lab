using CalypsoCad.Blueprints;
using CalypsoCad.Blueprints.DrawingPackage;
using CalypsoCad.Session;
using CalypsoCad.Structure;
using CalypsoCad.Viewport;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Export;

/// <summary>Acceptance / utility gates invoked by CLI flags and <see cref="HeadlessExportSuite"/>.</summary>
internal static class CalypsoAcceptanceGates
{
    public static int Blueprints(bool emitDetails = true)
    {
        try
        {
            var cadPath = Path.Combine(CalypsoLockGenerator.DefaultOutputDirectory, "calypso.cadjson");
            if (!File.Exists(cadPath))
                CalypsoLockGenerator.Generate();
            InternalsBlueprintEmitter.Emit(cadJsonPath: cadPath);
            ManufacturerSheetEmitter.Emit();
            if (emitDetails)
                DrawingPackageEmitter.Emit(cadJsonPath: cadPath); // refreshes details/ + docs/blueprints/

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    public static int StructureAudit()
    {
        try
        {
            var cadPath = Path.Combine(CalypsoLockGenerator.DefaultOutputDirectory, "calypso.cadjson");
            if (!File.Exists(cadPath))
                CalypsoLockGenerator.Generate();
            var doc = System.Text.Json.JsonSerializer.Deserialize<CadDocument>(
                File.ReadAllText(cadPath), CadJson.Options)
                ?? throw new InvalidOperationException("Failed to deserialize calypso.cadjson");
            var (json, ndjson, failures) = StructureAuditExporter.Write(doc);
            Console.WriteLine($"Wrote structure audit:{Environment.NewLine}{json}{Environment.NewLine}{ndjson}");
            Console.WriteLine(failures == 0
                ? "structure-audit: all bh-* groups pierce spine"
                : $"structure-audit: {failures} group(s) off spine");
            var asm = Validation.AssemblyValidation.Validate(doc);
            var unclassified = Publication.CadDocumentAdapter.Active.Unclassified.Count;
            Console.WriteLine(
                $"semantic registry: parts={asm.RegistryParts} bhPlates={asm.BulkheadPlates} frames={asm.FrameAssemblies} unclassified={unclassified} ok={asm.Ok}");
            if (asm.BulkheadPlates < 6)
            {
                Console.WriteLine("structure-audit FAIL: expected ≥6 semantic bulkhead plates");
                return 2;
            }

            if (unclassified > 0)
            {
                Console.WriteLine($"structure-audit FAIL: {unclassified} unclassified primary structure entit(y/ies)");
                return 3;
            }

            return failures == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    public static int StructureInspection()
    {
        Console.WriteLine("CalypsoCad structure inspection (semantic A–E)");
        var failures = 0;
        void Check(string name, bool ok, string detail = "")
        {
            if (ok)
            {
                Console.WriteLine($"  OK  {name}");
                return;
            }

            failures++;
            Console.WriteLine($"  FAIL {name}{(string.IsNullOrEmpty(detail) ? "" : ": " + detail)}");
        }

        try
        {
            return CalypsoHeadlessHost.WithViewport((session, renderer) =>
            {
                Publication.CadDocumentAdapter.LoadFromDocument(session.Document);
                var stills = HeadlessStructureInspectionExporter.Export(session, renderer);
                Check("inspection PNGs A–E", stills.Count >= 5, $"count={stills.Count}");

                var inventoryPath = Path.Combine(
                    CalypsoProjectPaths.RendersDirectory, "structure-inspection", "surface-inventory.json");
                Check("surface inventory", File.Exists(inventoryPath), inventoryPath);

                var knucklePath = Path.Combine(
                    CalypsoProjectPaths.RendersDirectory, "structure-inspection", "knuckle-continuity.json");
                Check("knuckle continuity report", File.Exists(knucklePath), knucklePath);

                if (File.Exists(knucklePath))
                {
                    var json = File.ReadAllText(knucklePath);
                    Check(
                        "knuckle continuity passed",
                        json.Contains("\"Passed\": true", StringComparison.Ordinal)
                        || json.Contains("\"passed\": true", StringComparison.OrdinalIgnoreCase),
                        "see knuckle-continuity.json");
                }

                Console.WriteLine(failures == 0
                    ? "CalypsoCad structure inspection OK"
                    : $"CalypsoCad structure inspection FAILED ({failures})");
                return failures == 0 ? 0 : 1;
            });
        }
        catch (Exception ex)
        {
            Check("structure inspection pipeline", false, ex.Message);
            Console.Error.WriteLine(ex);
            Console.WriteLine($"CalypsoCad structure inspection FAILED ({failures})");
            return 1;
        }
    }

    public static int HullFirst()
    {
        Console.WriteLine("CalypsoCad hull-first transverse skeleton");
        var failures = 0;
        void Check(string name, bool ok, string detail = "")
        {
            if (ok)
            {
                Console.WriteLine($"  OK  {name}");
                return;
            }

            failures++;
            Console.WriteLine($"  FAIL {name}{(string.IsNullOrEmpty(detail) ? "" : ": " + detail)}");
        }

        try
        {
            return CalypsoHeadlessHost.WithViewport((session, renderer) =>
            {
                var report = HullFirstValidation.Evaluate(session.Document);
                var reportPath = Path.Combine(CalypsoProjectPaths.RendersDirectory, "hull-first", "validation-report.md");
                HullFirstValidation.WriteMarkdown(report, reportPath);
                Console.WriteLine($"Validation → {reportPath}");
                foreach (var c in report.Checks)
                    Check(c.Name, c.Ok, c.Detail);

                var contourPath = FrameContourValidation.WriteReport(session.Document);
                Console.WriteLine($"Frame contour → {contourPath}");
                var annulus = session.Document.Entities.Count(e =>
                    e.Kind == "mesh"
                    && e.Name is not null
                    && e.Name.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
                    && e.Name.Contains("annulus", StringComparison.OrdinalIgnoreCase));
                Check("frame annulus meshes", annulus >= 8, $"count={annulus}");

                var stills = HeadlessHullFirstExporter.Export(session, renderer);
                Console.WriteLine($"Hull-first PNGs ({stills.Count}) → {Path.Combine(CalypsoProjectPaths.RendersDirectory, "hull-first")}");
                foreach (var path in stills)
                    Console.WriteLine($"  {path}");
                Check("hull-first PNGs written", stills.Count >= 9, $"count={stills.Count}");
                Check("hull-first gate", report.Ok);

                Console.WriteLine(failures == 0
                    ? "CalypsoCad hull-first OK"
                    : $"CalypsoCad hull-first FAILED ({failures})");
                return failures == 0 ? 0 : 1;
            });
        }
        catch (Exception ex)
        {
            Check("hull-first pipeline", false, ex.Message);
            Console.Error.WriteLine(ex);
            Console.WriteLine($"CalypsoCad hull-first FAILED ({failures})");
            return 1;
        }
    }

    public static int BulkheadMesh()
    {
        Console.WriteLine("CalypsoCad bulkhead polygon-mesh acceptance");
        try
        {
            return CalypsoHeadlessHost.WithViewport((session, renderer) =>
            {
                var plates = session.Document.Entities.Count(e =>
                    e.Kind == "mesh"
                    && e.Name is not null
                    && e.Name.StartsWith("bh-", StringComparison.OrdinalIgnoreCase)
                    && e.Name.Contains("-plate", StringComparison.OrdinalIgnoreCase));
                var voxelBoxes = session.Document.Entities.Count(e =>
                    e.Kind == "box"
                    && e.Name is not null
                    && e.Name.StartsWith("bh-", StringComparison.OrdinalIgnoreCase)
                    && (e.Name.Contains("-web-solid-", StringComparison.OrdinalIgnoreCase)
                        || e.Name.Contains("-web-", StringComparison.OrdinalIgnoreCase)));
                var seats = session.Document.Entities.Count(e =>
                    e.Kind == "box"
                    && e.Name is not null
                    && e.Name.Contains("-deckseat-", StringComparison.OrdinalIgnoreCase));
                Console.WriteLine($"  bh mesh plates={plates}  leftover bh voxel boxes={voxelBoxes}  deckseats={seats}");
                if (plates < 6 || voxelBoxes > 0)
                {
                    Console.WriteLine("  FAIL expected ≥6 mesh plates and 0 bh voxel boxes");
                    return 1;
                }

                var stills = HeadlessBulkheadMeshExporter.Export(session, renderer);
                Console.WriteLine(
                    $"Bulkhead-mesh PNGs ({stills.Count}) → {Path.Combine(CalypsoProjectPaths.RendersDirectory, "bulkhead-mesh")}");
                foreach (var path in stills)
                    Console.WriteLine($"  {path}");

                var validationPath = Path.Combine(
                    CalypsoProjectPaths.RendersDirectory,
                    "bulkhead-geometry",
                    "bulkhead-geometry-validation.json");
                if (!File.Exists(validationPath))
                {
                    Console.WriteLine("  FAIL missing bulkhead-geometry-validation.json (geometry gate skipped)");
                    return 3;
                }

                var failed = CalypsoLockGenerator.LastStructureMeshValidations
                    .Where(r => r.PartId.StartsWith("bh-", StringComparison.OrdinalIgnoreCase) && !r.Passed)
                    .ToList();
                if (failed.Count > 0)
                {
                    foreach (var f in failed)
                        Console.WriteLine($"  FAIL {f.PartId}: {string.Join("; ", f.Failures)}");
                    return 4;
                }

                var bhOk = CalypsoLockGenerator.LastStructureMeshValidations
                    .Count(r => r.PartId.StartsWith("bh-", StringComparison.OrdinalIgnoreCase) && r.Passed);
                Console.WriteLine($"  geometry gate: {bhOk} validated bh plates → {validationPath}");
                if (bhOk < 6)
                {
                    Console.WriteLine("  FAIL expected ≥6 validated bulkhead plates");
                    return 5;
                }

                // Face-on stills for all six + whole-ship pair.
                var need = new[]
                {
                    "00-whole-ship-no-cutaway.png",
                    "00-whole-ship-cutaway.png",
                    "bow-collision-01-face-on-solid.png",
                    "cross-fore-01-face-on-solid.png",
                    "cross-aft-01-face-on-solid.png",
                    "crew-aft-01-face-on-solid.png",
                    "eng-fore-01-face-on-solid.png",
                    "eng-aft-01-face-on-solid.png",
                };
                var meshDir = Path.Combine(CalypsoProjectPaths.RendersDirectory, "bulkhead-mesh");
                foreach (var n in need)
                {
                    if (!File.Exists(Path.Combine(meshDir, n)))
                    {
                        Console.WriteLine($"  FAIL missing diagnostic still {n}");
                        return 6;
                    }
                }

                return stills.Count >= need.Length ? 0 : 2;
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    public static int CuratedStills() =>
        CalypsoHeadlessHost.WithViewport((session, renderer) =>
        {
            var stills = HeadlessPngExporter.ExportViews(session, renderer, session.GeneratedDirectory);
            if (stills.Count == 0)
            {
                Console.Error.WriteLine("Headless PNG export produced no frames (Silk/GPU may be unavailable).");
                return 2;
            }

            Console.WriteLine($"Headless Silk PNG exports ({stills.Count}):");
            foreach (var path in stills)
                Console.WriteLine($"  {path}");
            return 0;
        });

    public static int ConstructionOrbit() =>
        CalypsoHeadlessHost.WithViewport((session, renderer) =>
        {
            var outputs = HeadlessConstructionOrbitExporter.Export(session, renderer);
            Console.WriteLine(
                $"Construction orbit exports ({outputs.Count}) → {CalypsoProjectPaths.ConstructionOrbitDirectory}");
            foreach (var path in outputs.Where(p =>
                         p.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                         || p.Contains("key-", StringComparison.OrdinalIgnoreCase)
                         || p.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase)))
                Console.WriteLine($"  {path}");
            return outputs.Any(p => p.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) ? 0 : 2;
        });

    public static int Walkthrough() =>
        CalypsoHeadlessHost.WithViewport((session, renderer) =>
        {
            var wt = HeadlessWalkthroughExporter.Export(session, renderer, session.GeneratedDirectory);
            if (wt.Count == 0)
            {
                Console.Error.WriteLine("Walkthrough export produced no frames (Silk/GPU may be unavailable).");
                return 2;
            }

            Console.WriteLine($"Walkthrough exports ({wt.Count}):");
            foreach (var path in wt)
                Console.WriteLine($"  {path}");
            return 0;
        });

    public static int GenerateOnly()
    {
        CalypsoHeadlessHost.EnsureGenerated();
        return 0;
    }

    public static int DeckGeometry()
    {
        Console.WriteLine("CalypsoCad whole-ship deck geometry");
        var failures = 0;
        void Check(string name, bool ok, string detail = "")
        {
            if (ok)
            {
                Console.WriteLine($"  OK  {name}");
                return;
            }

            failures++;
            Console.WriteLine($"  FAIL {name}{(string.IsNullOrEmpty(detail) ? "" : ": " + detail)}");
        }

        try
        {
            return CalypsoHeadlessHost.WithViewport((session, renderer) =>
            {
                var diag = DeckCadDiagnostics.Analyze(session.Document);
                var diagPath = Path.Combine(CalypsoProjectPaths.DeckGeometryDirectory, "cad-diagnostics.md");
                diag.WriteMarkdown(diagPath);
                Console.WriteLine($"CAD diagnostics → {diagPath}");
                foreach (var n in diag.Notes)
                    Console.WriteLine($"  · {n}");
                Check("no coplanar face overlaps", diag.OverlappingFacePairs == 0,
                    $"pairs={diag.OverlappingFacePairs}");
                Check("no duplicate sandwich names", diag.DuplicateNameCount == 0,
                    $"dups={diag.DuplicateNameCount}");
                Check("ribs inside face footprints", diag.RibsPastPeriEstimate == 0,
                    $"pastPeri={diag.RibsPastPeriEstimate}");
                Check("panel count sane (not 1m slices)", diag.ByDeck.Sum(d => d.FaceTops) <= 80,
                    $"faceTops={diag.ByDeck.Sum(d => d.FaceTops)}");

                var report = DeckGeometryValidation.Evaluate(session.Document);
                var reportPath = Path.Combine(CalypsoProjectPaths.DeckGeometryDirectory, "validation-report.md");
                DeckGeometryValidation.WriteReportMarkdown(report, reportPath);
                StructurePlacementLog.WriteMarkdown(
                    Path.Combine(CalypsoProjectPaths.DeckGeometryDirectory, "placement-failures.md"));
                Console.WriteLine($"Validation report → {reportPath}");
                foreach (var c in report.Checks)
                    Check(c.Name, c.Ok, c.Detail);

                var stills = HeadlessDeckGeometryExporter.Export(session, renderer);
                Console.WriteLine($"Deck PNGs ({stills.Count}) → {CalypsoProjectPaths.DeckGeometryDirectory}");
                foreach (var path in stills)
                    Console.WriteLine($"  {path}");
                Check("deck inspection PNGs written", stills.Count >= 3, $"count={stills.Count}");
                Check("deck geometry gate", report.Ok);

                Console.WriteLine(failures == 0
                    ? "CalypsoCad whole-ship deck geometry OK"
                    : $"CalypsoCad whole-ship deck geometry FAILED ({failures})");
                return failures == 0 ? 0 : 1;
            });
        }
        catch (Exception ex)
        {
            Check("deck geometry pipeline", false, ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    public static int StructuralBay()
    {
        Console.WriteLine("CalypsoCad two-bay structural proof (F24–F32)");
        var failures = 0;
        void Check(string name, bool ok, string detail = "")
        {
            if (ok)
            {
                Console.WriteLine($"  OK  {name}");
                return;
            }

            failures++;
            Console.WriteLine($"  FAIL {name}{(string.IsNullOrEmpty(detail) ? "" : ": " + detail)}");
        }

        try
        {
            var session = new CalypsoSession();
            session.RegenerateAndLoadStructuralBay();
            Console.WriteLine($"Wrote bay CAD → {session.GeneratedDirectory}");

            var joints = StructuralAssemblyGraph.ReadJointsFromDocument(session.Document);
            const float loa = 69f;
            var report = StructuralBayGeometry.Evaluate(session.Document, joints, loa);
            var reportPath = Path.Combine(CalypsoProjectPaths.StructuralBayDirectory, "validation-report.md");
            StructuralBayGeometry.WriteReportMarkdown(report, reportPath);
            Console.WriteLine($"Validation report → {reportPath}");
            foreach (var c in report.Checks)
                Check(c.Name, c.Ok, c.Detail);

            var renderer = new CalypsoRenderer(session);
            renderer.Fit();
            var stills = HeadlessStructuralBayExporter.Export(session, renderer);
            Console.WriteLine($"Bay PNGs ({stills.Count}) → {CalypsoProjectPaths.StructuralBayDirectory}");
            foreach (var path in stills)
                Console.WriteLine($"  {path}");
            Check("diagnostic PNGs written", stills.Count >= 4, $"count={stills.Count}");
            Check("two-bay geometric gate", report.Ok);

            Console.WriteLine(failures == 0
                ? "CalypsoCad two-bay structural proof OK"
                : $"CalypsoCad two-bay structural proof FAILED ({failures})");
            return failures == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Check("structural bay pipeline", false, ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
