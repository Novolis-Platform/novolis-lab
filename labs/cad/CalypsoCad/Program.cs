using CalypsoCad.Blueprints;
using CalypsoCad.Agent;
using CalypsoCad.Generation;
using Novolis.Cad.Primitives;
using CalypsoCad.Host;
using CalypsoCad.Rendering;
using CalypsoCad.Export;
using CalypsoCad.Shell;
using Novolis.Agent.Surface;

namespace CalypsoCad;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var generateOnly = args.Any(a => string.Equals(a, "--generate-only", StringComparison.OrdinalIgnoreCase));
        var headless = args.Any(a => string.Equals(a, "--headless", StringComparison.OrdinalIgnoreCase));
        var walkthrough = args.Any(a => string.Equals(a, "--walkthrough", StringComparison.OrdinalIgnoreCase));
        var constructionOrbit = args.Any(a =>
            string.Equals(a, "--construction-orbit", StringComparison.OrdinalIgnoreCase));
        var jsonOnly = args.Any(a => string.Equals(a, "--json-only", StringComparison.OrdinalIgnoreCase));
        var acceptance = args.Any(a => string.Equals(a, "--acceptance", StringComparison.OrdinalIgnoreCase));
        var blueprints = args.Any(a => string.Equals(a, "--blueprints", StringComparison.OrdinalIgnoreCase));
        var blueprintDetails = args.Any(a => string.Equals(a, "--blueprint-details", StringComparison.OrdinalIgnoreCase));
        var structureAudit = args.Any(a => string.Equals(a, "--structure-audit", StringComparison.OrdinalIgnoreCase));
        var explorer = args.Any(a => string.Equals(a, "--explorer", StringComparison.OrdinalIgnoreCase));
        var silkOnly = args.Any(a => string.Equals(a, "--silk", StringComparison.OrdinalIgnoreCase));
        var mcp = args.Any(a => string.Equals(a, "--mcp", StringComparison.OrdinalIgnoreCase));
        var stdio = args.Any(a => string.Equals(a, "--stdio", StringComparison.OrdinalIgnoreCase));

        if (mcp || stdio)
        {
            Environment.ExitCode = RunAgent(mcp).GetAwaiter().GetResult();
            return;
        }

        if (structureAudit)
        {
            Environment.ExitCode = RunStructureAudit();
            return;
        }

        if (blueprints || blueprintDetails)
        {
            Environment.ExitCode = RunBlueprints(emitDetails: blueprints || blueprintDetails);
            return;
        }

        if (acceptance)
        {
            Environment.ExitCode = RunAcceptance();
            return;
        }

        var fabRelease = args.Any(a => string.Equals(a, "--fab-release", StringComparison.OrdinalIgnoreCase));
        if (fabRelease)
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

        var dnvReview = args.Any(a => string.Equals(a, "--dnv-review", StringComparison.OrdinalIgnoreCase));
        if (dnvReview)
        {
            var withHeadless = args.Any(a => string.Equals(a, "--headless", StringComparison.OrdinalIgnoreCase));
            try
            {
                var html = CalypsoDnvReviewEmitter.Emit(runHeadlessExports: withHeadless);
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

        var withHeadlessCanvas = args.Any(a => string.Equals(a, "--headless", StringComparison.OrdinalIgnoreCase));
        if (TryEmitRenderCanvas(args, "--emit-mfg-angles-canvas", "calypso-mfg-angles.canvas.tsx", withHeadlessCanvas,
                CalypsoRenderCanvasEmitter.EmitMfgAngles))
            return;
        if (TryEmitRenderCanvas(args, "--emit-mfg-002-renders-canvas", "calypso-mfg-002-renders.canvas.tsx", withHeadlessCanvas,
                CalypsoRenderCanvasEmitter.EmitMfg002FullRenders))
            return;

        var buildSteps = args.Any(a => string.Equals(a, "--build-steps", StringComparison.OrdinalIgnoreCase));
        var emitBuildDeckIdx = Array.FindIndex(args, a =>
            string.Equals(a, "--emit-build-steps-canvas", StringComparison.OrdinalIgnoreCase));
        if (buildSteps || emitBuildDeckIdx >= 0)
        {
            Environment.ExitCode = RunBuildSteps(
                exportPng: buildSteps || withHeadlessCanvas,
                emitCanvas: emitBuildDeckIdx >= 0,
                canvasPath: emitBuildDeckIdx >= 0 && emitBuildDeckIdx + 1 < args.Length && !args[emitBuildDeckIdx + 1].StartsWith('-')
                    ? args[emitBuildDeckIdx + 1]
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".cursor", "projects", "d-novolis", "canvases", "calypso-structure-build-deck.canvas.tsx"));
            return;
        }

        if (constructionOrbit)
        {
            Environment.ExitCode = RunConstructionOrbit();
            return;
        }

        if (generateOnly || headless || walkthrough)
        {
            Environment.ExitCode = RunHeadless(
                exportPng: headless && !jsonOnly,
                walkthrough: walkthrough);
            return;
        }

        var session = new CalypsoSession();
        session.RegenerateAndLoad();
        var renderer = new CalypsoRenderer(session);
        renderer.Fit();
        renderer.SyncInteriorFromSelection();
        // Human default: Avalonia chrome + Silk 3D. CI/agents: --silk for GLFW-only.
        if (silkOnly && !explorer)
            CalypsoSilkHost.Run(session, renderer);
        else
            CalypsoExplorerHost.Run(session, renderer);
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
