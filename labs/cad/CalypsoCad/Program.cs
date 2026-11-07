using CalypsoCad.Blueprints;
using CalypsoCad.Agent;
using Novolis.Cad.Primitives;
using CalypsoCad.Session;
using CalypsoCad.Viewport;
using CalypsoCad.Export;
using CalypsoCad.Explorer;
using Novolis.Agent.Surface;
using CalypsoCad.Structure;
using CalypsoCad.Fabrication;
using CalypsoCad.Review;
using CalypsoCad.Canvas;

namespace CalypsoCad;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Two product modes:
        //   GUI      — no CLI work flags (optional --explorer / --silk)
        //   Headless — --headless runs the full export suite (stills, video, blueprints, descriptors)
        // Focused CLI flags remain for iteration without opening the GUI.
        var headless = HasFlag(args, "--headless");
        var explorer = HasFlag(args, "--explorer");
        var silkOnly = HasFlag(args, "--silk");
        var mcp = HasFlag(args, "--mcp");
        var stdio = HasFlag(args, "--stdio");

        if (mcp || stdio)
        {
            Environment.ExitCode = RunAgent(mcp).GetAwaiter().GetResult();
            return;
        }

        if (headless)
        {
            Environment.ExitCode = RunFullHeadlessSuite(args);
            return;
        }

        if (HasFlag(args, "--structure-audit"))
        {
            Environment.ExitCode = RunStructureAudit();
            return;
        }

        if (HasFlag(args, "--structural-bay"))
        {
            Environment.ExitCode = RunStructuralBay();
            return;
        }

        if (HasFlag(args, "--deck-geometry"))
        {
            Environment.ExitCode = RunDeckGeometry();
            return;
        }

        if (HasFlag(args, "--hull-first"))
        {
            Environment.ExitCode = RunHullFirst();
            return;
        }

        if (HasFlag(args, "--bulkhead-mesh"))
        {
            Environment.ExitCode = RunBulkheadMesh();
            return;
        }

        if (HasFlag(args, "--blueprints") || HasFlag(args, "--blueprint-details"))
        {
            Environment.ExitCode = RunBlueprints(emitDetails: true);
            return;
        }

        if (HasFlag(args, "--acceptance"))
        {
            Environment.ExitCode = RunAcceptance();
            return;
        }

        if (HasFlag(args, "--fab-release"))
        {
            try
            {
                var html = CalypsoFabReleaseEmitter.Emit();
                Console.WriteLine($"Wrote CAL-MFG-002 fabrication release:{Environment.NewLine}{html}");
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                Environment.ExitCode = 1;
            }

            return;
        }

        if (HasFlag(args, "--dnv-review"))
        {
            try
            {
                var html = CalypsoDnvReviewEmitter.Emit(runHeadlessExports: false);
                Console.WriteLine($"Wrote DNV-style design review package:{Environment.NewLine}{html}");
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                Environment.ExitCode = 1;
            }

            return;
        }

        var emitCanvasIdx = Array.FindIndex(args, a =>
            string.Equals(a, "--emit-functional-canvas", StringComparison.OrdinalIgnoreCase));
        if (emitCanvasIdx >= 0)
        {
            var path = emitCanvasIdx + 1 < args.Length && !args[emitCanvasIdx + 1].StartsWith('-')
                ? args[emitCanvasIdx + 1]
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".cursor", "projects", "d-novolis", "canvases", "calypso-functional-vessel.canvas.tsx");
            var written = CalypsoFunctionalCanvasEmitter.Emit(path);
            Console.WriteLine($"Wrote functional vessel canvas:{Environment.NewLine}{written}");
            return;
        }

        if (TryEmitRenderCanvas(args, "--emit-mfg-angles-canvas", "calypso-mfg-angles.canvas.tsx", runHeadless: false,
                CalypsoRenderCanvasEmitter.EmitMfgAngles))
            return;
        if (TryEmitRenderCanvas(args, "--emit-mfg-002-renders-canvas", "calypso-mfg-002-renders.canvas.tsx", runHeadless: false,
                CalypsoRenderCanvasEmitter.EmitMfg002FullRenders))
            return;

        var buildSteps = HasFlag(args, "--build-steps");
        var emitBuildDeckIdx = Array.FindIndex(args, a =>
            string.Equals(a, "--emit-build-steps-canvas", StringComparison.OrdinalIgnoreCase));
        if (buildSteps || emitBuildDeckIdx >= 0)
        {
            Environment.ExitCode = RunBuildSteps(
                exportPng: buildSteps,
                emitCanvas: emitBuildDeckIdx >= 0,
                canvasPath: emitBuildDeckIdx >= 0 && emitBuildDeckIdx + 1 < args.Length && !args[emitBuildDeckIdx + 1].StartsWith('-')
                    ? args[emitBuildDeckIdx + 1]
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".cursor", "projects", "d-novolis", "canvases", "calypso-structure-build-deck.canvas.tsx"));
            return;
        }

        if (HasFlag(args, "--construction-orbit"))
        {
            Environment.ExitCode = RunConstructionOrbit();
            return;
        }

        if (HasFlag(args, "--walkthrough"))
        {
            Environment.ExitCode = RunHeadless(exportPng: false, walkthrough: true);
            return;
        }

        if (HasFlag(args, "--stills"))
        {
            Environment.ExitCode = RunHeadless(exportPng: true, walkthrough: false);
            return;
        }

        if (HasFlag(args, "--generate-only"))
        {
            Environment.ExitCode = RunHeadless(exportPng: false, walkthrough: false);
            return;
        }

        // GUI mode — Avalonia chrome + Silk 3D (or --silk for GLFW-only).
        var session = new CalypsoSession();
        session.RegenerateAndLoad();
        var renderer = new CalypsoRenderer(session);
        renderer.Fit();
        renderer.SyncInteriorFromSelection();
        if (silkOnly && !explorer)
            CalypsoSilkHost.Run(session, renderer);
        else
            CalypsoExplorerHost.Run(session, renderer);
    }

    private static bool HasFlag(string[] args, string flag) =>
        args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// CLI-only product mode: regenerate CAD, then emit blueprints, descriptors, 8K stills, and native videos.
    /// </summary>
    private static int RunFullHeadlessSuite(string[] args)
    {
        Console.WriteLine("CalypsoCad headless suite (CLI) — stills @8K, video @4K native Silk");
        var jsonOnly = HasFlag(args, "--json-only");
        var failures = 0;

        void Step(string name, Func<int> run)
        {
            Console.WriteLine($"── {name}");
            try
            {
                var code = run();
                if (code != 0)
                {
                    failures++;
                    Console.WriteLine($"  FAIL {name} (exit {code})");
                }
                else
                {
                    Console.WriteLine($"  OK  {name}");
                }
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"  FAIL {name}: {ex.Message}");
                Console.Error.WriteLine(ex);
            }
        }

        Step("generate CAD companions", () =>
        {
            var dir = CalypsoLockGenerator.Generate();
            Console.WriteLine($"  → {dir}");
            return File.Exists(Path.Combine(dir, "calypso.cadjson")) ? 0 : 2;
        });

        if (jsonOnly)
            return failures == 0 ? 0 : 1;

        Step("blueprints + manufacturer sheets", () => RunBlueprints(emitDetails: true));
        Step("fabrication release (CAL-MFG-002)", () =>
        {
            var html = CalypsoFabReleaseEmitter.Emit();
            Console.WriteLine($"  → {html}");
            return 0;
        });
        Step("structure audit descriptors", () => RunStructureAudit());
        Step("DNV design-review package", () =>
        {
            var html = CalypsoDnvReviewEmitter.Emit(runHeadlessExports: false);
            Console.WriteLine($"  → {html}");
            return 0;
        });
        Step("hull-first Views A–D", () => RunHullFirst());
        Step("bulkhead polygon-mesh views", () => RunBulkheadMesh());
        Step("curated 8K stills", () => RunHeadless(exportPng: true, walkthrough: false));
        Step("construction-orbit 4K video", () => RunConstructionOrbit());
        Step("walkthrough 4K video", () => RunHeadless(exportPng: false, walkthrough: true));

        if (HasFlag(args, "--build-steps"))
            Step("build-step stills", () => RunBuildSteps(exportPng: true, emitCanvas: false, canvasPath: ""));

        Console.WriteLine(failures == 0
            ? "CalypsoCad headless suite OK"
            : $"CalypsoCad headless suite FAILED ({failures} step(s))");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>Polygonal heavy-BH solids: regenerate → mesh views + IML section compare.</summary>
    private static int RunBulkheadMesh()
    {
        Console.WriteLine("CalypsoCad bulkhead polygon-mesh acceptance");
        try
        {
            var session = new CalypsoSession();
            session.RegenerateAndLoad();
            Console.WriteLine($"Wrote CAD → {session.GeneratedDirectory}");

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
                Console.WriteLine($"  FAIL expected ≥6 mesh plates and 0 bh voxel boxes");
                return 1;
            }

            var renderer = new CalypsoRenderer(session);
            renderer.Fit();
            var stills = HeadlessBulkheadMeshExporter.Export(session, renderer);
            Console.WriteLine($"Bulkhead-mesh PNGs ({stills.Count}) → {Path.Combine(CalypsoProjectPaths.RendersDirectory, "bulkhead-mesh")}");
            foreach (var path in stills)
                Console.WriteLine($"  {path}");
            return stills.Count >= 4 ? 0 : 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    /// <summary>Hull-first: regenerate → transverse skeleton validation → Views A–D.</summary>
    private static int RunHullFirst()
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
            var session = new CalypsoSession();
            session.RegenerateAndLoad();
            Console.WriteLine($"Wrote CAD → {session.GeneratedDirectory}");

            var report = HullFirstValidation.Evaluate(session.Document);
            var reportPath = Path.Combine(CalypsoProjectPaths.RendersDirectory, "hull-first", "validation-report.md");
            HullFirstValidation.WriteMarkdown(report, reportPath);
            Console.WriteLine($"Validation → {reportPath}");
            foreach (var c in report.Checks)
                Check(c.Name, c.Ok, c.Detail);

            var renderer = new CalypsoRenderer(session);
            renderer.Fit();
            var stills = HeadlessHullFirstExporter.Export(session, renderer);
            Console.WriteLine($"Hull-first PNGs ({stills.Count}) → {Path.Combine(CalypsoProjectPaths.RendersDirectory, "hull-first")}");
            foreach (var path in stills)
                Console.WriteLine($"  {path}");
            Check("hull-first PNGs written", stills.Count >= 6, $"count={stills.Count}");
            Check("hull-first gate", report.Ok);
        }
        catch (Exception ex)
        {
            Check("hull-first pipeline", false, ex.Message);
            Console.Error.WriteLine(ex);
        }

        Console.WriteLine(failures == 0
            ? "CalypsoCad hull-first OK"
            : $"CalypsoCad hull-first FAILED ({failures})");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>Whole-ship deck footprints: regenerate → geometric validation → inspection PNGs.</summary>
    private static int RunDeckGeometry()
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
            var session = new CalypsoSession();
            session.RegenerateAndLoad();
            Console.WriteLine($"Wrote CAD → {session.GeneratedDirectory}");

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

            var renderer = new CalypsoRenderer(session);
            renderer.Fit();
            var stills = HeadlessDeckGeometryExporter.Export(session, renderer);
            Console.WriteLine($"Deck PNGs ({stills.Count}) → {CalypsoProjectPaths.DeckGeometryDirectory}");
            foreach (var path in stills)
                Console.WriteLine($"  {path}");
            Check("deck inspection PNGs written", stills.Count >= 3, $"count={stills.Count}");
            Check("deck geometry gate", report.Ok);
        }
        catch (Exception ex)
        {
            Check("deck geometry pipeline", false, ex.Message);
            Console.Error.WriteLine(ex);
        }

        Console.WriteLine(failures == 0
            ? "CalypsoCad whole-ship deck geometry OK"
            : $"CalypsoCad whole-ship deck geometry FAILED ({failures})");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>Two-bay midbody proof: generate → geometric validation → four diagnostic PNGs.</summary>
    private static int RunStructuralBay()
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
            var loa = session.Document.Properties is not null
                      && session.Document.Properties.TryGetValue("shipEnvelope", out var env)
                      && env.ValueKind == System.Text.Json.JsonValueKind.Object
                      && env.TryGetProperty("loa", out var loaEl)
                ? (float)loaEl.GetDouble()
                : 69f;
            // Prefer lock LOA from entities camera / default.
            loa = 69f;
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
        }
        catch (Exception ex)
        {
            Check("structural bay pipeline", false, ex.Message);
            Console.Error.WriteLine(ex);
        }

        Console.WriteLine(failures == 0
            ? "CalypsoCad two-bay structural proof OK"
            : $"CalypsoCad two-bay structural proof FAILED ({failures})");
        return failures == 0 ? 0 : 1;
    }

    private static int RunStructureAudit()
    {
        try
        {
            var cadPath = Path.Combine(CalypsoLockGenerator.DefaultOutputDirectory, "calypso.cadjson");
            if (!File.Exists(cadPath))
                CalypsoLockGenerator.Generate();
            var doc = System.Text.Json.JsonSerializer.Deserialize<Novolis.Cad.Primitives.CadDocument>(
                File.ReadAllText(cadPath), CadJson.Options)
                ?? throw new InvalidOperationException("Failed to deserialize calypso.cadjson");
            var (json, ndjson, failures) = StructureAuditExporter.Write(doc);
            Console.WriteLine($"Wrote structure audit:{Environment.NewLine}{json}{Environment.NewLine}{ndjson}");
            Console.WriteLine(failures == 0
                ? "structure-audit: all bh-* groups pierce spine"
                : $"structure-audit: {failures} group(s) off spine");
            return failures == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int RunBlueprints(bool emitDetails = true)
    {
        try
        {
            var cadPath = Path.Combine(CalypsoLockGenerator.DefaultOutputDirectory, "calypso.cadjson");
            if (!File.Exists(cadPath))
                CalypsoLockGenerator.Generate();
            CalypsoCad.Blueprints.InternalsBlueprintEmitter.Emit(cadJsonPath: cadPath);
            CalypsoCad.Blueprints.ManufacturerSheetEmitter.Emit();
            if (emitDetails)
                CalypsoCad.Blueprints.StructureDetailBlueprintEmitter.Emit(cadJsonPath: cadPath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    /// <summary>Seed generate → inject exterior → regenerate → assert exterior preserved + lock metrics.</summary>
    private static int RunAcceptance()
    {
        Console.WriteLine("CalypsoCad acceptance");
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

        var dir = CalypsoLockGenerator.Generate();
        var cadPath = Path.Combine(dir, "calypso.cadjson");
        Check("generate calypso.cadjson", File.Exists(cadPath));

        try
        {
            CalypsoFabReleaseEmitter.Emit(generatedCadDirectory: dir);
            var fabRoot = Path.Combine(dir, CalypsoFabReleaseEmitter.PackageId);
            Check(
                "emit CAL-MFG-002",
                Directory.Exists(fabRoot)
                && File.Exists(Path.Combine(fabRoot, "calypso-fab-manifest.json")));
        }
        catch (Exception ex)
        {
            Check("emit CAL-MFG-002", false, ex.Message);
        }

        var doc = System.Text.Json.JsonSerializer.Deserialize<Novolis.Cad.Primitives.CadDocument>(
            File.ReadAllText(cadPath), CadJson.Options)
            ?? throw new InvalidOperationException("Failed to deserialize calypso.cadjson");

        foreach (var check in CalypsoAcceptance.Evaluate(doc, dir))
            Check(check.Name, check.Ok, check.Detail);

        doc.Entities.Add(new Novolis.Cad.Primitives.CadEntity
        {
            Kind = "box",
            Name = "ext-acceptance-hull",
            Center = [0, 6, 32],
            HalfExtents = [10f, 3f, 5f],
            Properties = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["exterior"] = System.Text.Json.JsonSerializer.SerializeToElement(true),
                ["handAuthored"] = System.Text.Json.JsonSerializer.SerializeToElement(true),
            },
        });
        File.WriteAllText(cadPath, System.Text.Json.JsonSerializer.Serialize(doc, CadJson.Options));

        CalypsoLockGenerator.Generate();
        var after = System.Text.Json.JsonSerializer.Deserialize<Novolis.Cad.Primitives.CadDocument>(
            File.ReadAllText(cadPath), CadJson.Options)
            ?? throw new InvalidOperationException("Failed to deserialize after regenerate");
        Check(
            "regenerate preserves exterior",
            after.Entities.Any(e => string.Equals(e.Name, "ext-acceptance-hull", StringComparison.OrdinalIgnoreCase)));

        after.Entities.RemoveAll(e =>
            string.Equals(e.Name, "ext-acceptance-hull", StringComparison.OrdinalIgnoreCase));
        File.WriteAllText(cadPath, System.Text.Json.JsonSerializer.Serialize(after, CadJson.Options));
        CalypsoLockGenerator.Generate();
        var clean = System.Text.Json.JsonSerializer.Deserialize<Novolis.Cad.Primitives.CadDocument>(
            File.ReadAllText(cadPath), CadJson.Options)
            ?? throw new InvalidOperationException("Failed to deserialize clean regenerate");
        Check(
            "acceptance probe stripped",
            !clean.Entities.Any(e => string.Equals(e.Name, "ext-acceptance-hull", StringComparison.OrdinalIgnoreCase)));

        Console.WriteLine(failures == 0 ? "CalypsoCad acceptance OK" : $"CalypsoCad acceptance FAILED ({failures})");
        return failures == 0 ? 0 : 1;
    }

    private static bool TryEmitRenderCanvas(
        string[] args,
        string flag,
        string defaultFileName,
        bool runHeadless,
        Func<string, string?, bool, string> emit)
    {
        var idx = Array.FindIndex(args, a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
            return false;
        var path = idx + 1 < args.Length && !args[idx + 1].StartsWith('-')
            ? args[idx + 1]
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cursor", "projects", "d-novolis", "canvases", defaultFileName);
        try
        {
            var written = emit(path, null, runHeadless);
            Console.WriteLine($"Wrote render canvas:{Environment.NewLine}{written}");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Environment.ExitCode = 1;
            return true;
        }
    }

    private static int RunBuildSteps(bool exportPng, bool emitCanvas, string canvasPath)
    {
        try
        {
            if (exportPng)
            {
                var session = new CalypsoSession();
                session.RegenerateAndLoad();
                var renderer = new CalypsoRenderer(session);
                renderer.Fit();
                var stills = HeadlessBuildStepExporter.Export(session, renderer);
                Console.WriteLine($"Build-step PNGs ({stills.Count}) → {CalypsoProjectPaths.BuildStepsDirectory}");
                foreach (var path in stills)
                    Console.WriteLine($"  {path}");
                if (stills.Count == 0)
                    return 2;
            }

            if (emitCanvas)
            {
                var written = CalypsoBuildStepCanvasEmitter.Emit(canvasPath, runExport: false);
                Console.WriteLine($"Wrote build-step slide deck:{Environment.NewLine}{written}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int RunConstructionOrbit()
    {
        try
        {
            var dir = CalypsoLockGenerator.Generate();
            Console.WriteLine($"Wrote Calypso CAD companions to:{Environment.NewLine}{dir}");
            var session = new CalypsoSession();
            session.RegenerateAndLoad();
            var renderer = new CalypsoRenderer(session);
            renderer.Fit();
            renderer.SyncInteriorFromSelection();
            var outputs = HeadlessConstructionOrbitExporter.Export(session, renderer);
            Console.WriteLine($"Construction orbit exports ({outputs.Count}) → {CalypsoProjectPaths.ConstructionOrbitDirectory}");
            foreach (var path in outputs.Where(p =>
                         p.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                         || p.Contains("key-", StringComparison.OrdinalIgnoreCase)
                         || p.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase)))
                Console.WriteLine($"  {path}");
            return outputs.Any(p => p.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) ? 0 : 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    /// <summary>Generate CAD companions and optionally export plan/orbit/interior PNGs or a walkthrough via Silk.</summary>
    private static int RunHeadless(bool exportPng, bool walkthrough)
    {
        var dir = CalypsoLockGenerator.Generate();
        Console.WriteLine($"Wrote Calypso Rev H (lock LOA 69) CAD companions to:{Environment.NewLine}{dir}");

        if (!exportPng && !walkthrough)
            return 0;

        try
        {
            var session = new CalypsoSession();
            session.RegenerateAndLoad();
            var renderer = new CalypsoRenderer(session);
            renderer.Fit();
            renderer.SyncInteriorFromSelection();

            var saved = new List<string>();

            if (exportPng)
            {
                var stills = HeadlessPngExporter.ExportViews(session, renderer, session.GeneratedDirectory);
                saved.AddRange(stills);
                if (stills.Count == 0)
                {
                    Console.Error.WriteLine("Headless PNG export produced no frames (Silk/GPU may be unavailable).");
                    return 2;
                }

                Console.WriteLine($"Headless Silk PNG exports ({stills.Count}):");
                foreach (var path in stills)
                    Console.WriteLine($"  {path}");
            }

            if (walkthrough)
            {
                var wt = HeadlessWalkthroughExporter.Export(session, renderer, session.GeneratedDirectory);
                saved.AddRange(wt);
                if (wt.Count == 0)
                {
                    Console.Error.WriteLine("Walkthrough export produced no frames (Silk/GPU may be unavailable).");
                    return 2;
                }

                Console.WriteLine($"Walkthrough exports ({wt.Count}):");
                foreach (var path in wt)
                    Console.WriteLine($"  {path}");
            }

            return saved.Count > 0 ? 0 : 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Headless export failed: {ex.Message}");
            return 2;
        }
    }

    private static async Task<int> RunAgent(bool mcp)
    {
        var host = new CalypsoCadAgentHost();
        var definition = AgentSurfaceDefinition.From<ICalypsoCadSurface>();
        if (mcp)
        {
            await using var transport = new AgentMcpStdioTransport(host, definition);
            await transport.RunUntilEofAsync(CancellationToken.None).ConfigureAwait(false);
            return 0;
        }

        await using var stdio = new AgentStdioHost(host);
        await stdio.RunUntilEofAsync(CancellationToken.None).ConfigureAwait(false);
        return 0;
    }
}
