using CalypsoCad.Agent;
using CalypsoCad.Canvas;
using CalypsoCad.Export;
using CalypsoCad.Explorer;
using CalypsoCad.Fabrication;
using CalypsoCad.Host;
using CalypsoCad.Review;
using CalypsoCad.Session;
using CalypsoCad.Structure;
using CalypsoCad.Viewport;
using Novolis.Agent.Surface;
using Novolis.Cad.Primitives;

namespace CalypsoCad;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Two product modes: GUI (default) or --headless-export.
        if (HasFlag(args, "--mcp") || HasFlag(args, "--stdio"))
        {
            Environment.ExitCode = RunAgent(HasFlag(args, "--mcp")).GetAwaiter().GetResult();
            return;
        }

        if (HasFlag(args, "--headless-export"))
        {
            Environment.ExitCode = HeadlessExportSuite.Run(jsonOnly: HasFlag(args, "--json-only"));
            return;
        }

        // Utility flags (skills / gates) — not product modes.
        if (HasFlag(args, "--structure-audit"))
        {
            Environment.ExitCode = CalypsoAcceptanceGates.StructureAudit();
            return;
        }

        if (HasFlag(args, "--structural-bay"))
        {
            Environment.ExitCode = CalypsoAcceptanceGates.StructuralBay();
            return;
        }

        if (HasFlag(args, "--deck-geometry"))
        {
            Environment.ExitCode = CalypsoAcceptanceGates.DeckGeometry();
            return;
        }

        if (HasFlag(args, "--hull-first"))
        {
            Environment.ExitCode = CalypsoAcceptanceGates.HullFirst();
            return;
        }

        if (HasFlag(args, "--structure-inspection"))
        {
            Environment.ExitCode = CalypsoAcceptanceGates.StructureInspection();
            return;
        }

        if (HasFlag(args, "--frame-interior"))
        {
            Environment.ExitCode = CalypsoAcceptanceGates.FrameInteriorInspection();
            return;
        }

        if (HasFlag(args, "--bulkhead-mesh"))
        {
            Environment.ExitCode = CalypsoAcceptanceGates.BulkheadMesh();
            return;
        }

        if (HasFlag(args, "--blueprints") || HasFlag(args, "--blueprint-details"))
        {
            Environment.ExitCode = CalypsoAcceptanceGates.Blueprints(emitDetails: true);
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

        if (TryEmitRenderCanvas(args, "--emit-mfg-angles-canvas", "calypso-mfg-angles.canvas.tsx",
                CalypsoRenderCanvasEmitter.EmitMfgAngles))
            return;
        if (TryEmitRenderCanvas(args, "--emit-mfg-002-renders-canvas", "calypso-mfg-002-renders.canvas.tsx",
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
                canvasPath: emitBuildDeckIdx >= 0 && emitBuildDeckIdx + 1 < args.Length
                            && !args[emitBuildDeckIdx + 1].StartsWith('-')
                    ? args[emitBuildDeckIdx + 1]
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".cursor", "projects", "d-novolis", "canvases",
                        "calypso-structure-build-deck.canvas.tsx"));
            return;
        }

        if (HasFlag(args, "--generate-only"))
        {
            Environment.ExitCode = CalypsoAcceptanceGates.GenerateOnly();
            return;
        }

        // GUI mode — default: thin CadSessionService host (CadEditorSurface).
        // --explorer / --silk keep the legacy Silk inspection shell.
        var silkOnly = HasFlag(args, "--silk");
        var explorer = HasFlag(args, "--explorer");
        if (silkOnly || explorer)
        {
            var session = new CalypsoSession();
            session.RegenerateAndLoad();
            var renderer = new CalypsoRenderer(session);
            renderer.Fit();
            renderer.SyncInteriorFromSelection();
            if (silkOnly && !explorer)
                CalypsoSilkHost.Run(session, renderer);
            else
                CalypsoExplorerHost.Run(session, renderer);
            return;
        }

        var dataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "CalypsoCad");
        var cadSession = CalypsoCadComposition.CreateSession(dataRoot);
        CalypsoCadHost.Run(cadSession);
    }

    private static bool HasFlag(string[] args, string flag) =>
        args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

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

        Check("hierarchy adapter metadata", Tests.CalypsoHierarchyAdapterTests.Run(out var adapterDetail), adapterDetail);

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

        var doc = System.Text.Json.JsonSerializer.Deserialize<CadDocument>(
            File.ReadAllText(cadPath), CadJson.Options)
            ?? throw new InvalidOperationException("Failed to deserialize calypso.cadjson");

        foreach (var check in CalypsoAcceptance.Evaluate(doc, dir))
            Check(check.Name, check.Ok, check.Detail);

        doc.Entities.Add(new CadEntity
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
        var after = System.Text.Json.JsonSerializer.Deserialize<CadDocument>(
            File.ReadAllText(cadPath), CadJson.Options)
            ?? throw new InvalidOperationException("Failed to deserialize after regenerate");
        Check(
            "regenerate preserves exterior",
            after.Entities.Any(e => string.Equals(e.Name, "ext-acceptance-hull", StringComparison.OrdinalIgnoreCase)));

        after.Entities.RemoveAll(e =>
            string.Equals(e.Name, "ext-acceptance-hull", StringComparison.OrdinalIgnoreCase));
        File.WriteAllText(cadPath, System.Text.Json.JsonSerializer.Serialize(after, CadJson.Options));
        CalypsoLockGenerator.Generate();
        var clean = System.Text.Json.JsonSerializer.Deserialize<CadDocument>(
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
            var written = emit(path, null, false);
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
                var code = CalypsoHeadlessHost.WithViewport((session, renderer) =>
                {
                    var stills = HeadlessBuildStepExporter.Export(session, renderer);
                    Console.WriteLine($"Build-step PNGs ({stills.Count}) → {CalypsoProjectPaths.BuildStepsDirectory}");
                    foreach (var path in stills)
                        Console.WriteLine($"  {path}");
                    return stills.Count == 0 ? 2 : 0;
                });
                if (code != 0)
                    return code;
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
