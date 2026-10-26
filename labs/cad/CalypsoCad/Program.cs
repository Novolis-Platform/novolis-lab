using Avalonia;
using CalypsoCad.Agent;
using CalypsoCad.Generation;
using CalypsoCad.Models;
using CalypsoCad.Services;
using Novolis.Agent.Surface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CalypsoCad;

internal static class Program
{
    internal static IHost ApplicationHost { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        var generateOnly = args.Any(a => string.Equals(a, "--generate-only", StringComparison.OrdinalIgnoreCase));
        var headless = args.Any(a => string.Equals(a, "--headless", StringComparison.OrdinalIgnoreCase));
        var walkthrough = args.Any(a => string.Equals(a, "--walkthrough", StringComparison.OrdinalIgnoreCase));
        var jsonOnly = args.Any(a => string.Equals(a, "--json-only", StringComparison.OrdinalIgnoreCase));
        var acceptance = args.Any(a => string.Equals(a, "--acceptance", StringComparison.OrdinalIgnoreCase));
        var blueprints = args.Any(a => string.Equals(a, "--blueprints", StringComparison.OrdinalIgnoreCase));
        var mcp = args.Any(a => string.Equals(a, "--mcp", StringComparison.OrdinalIgnoreCase));
        var stdio = args.Any(a => string.Equals(a, "--stdio", StringComparison.OrdinalIgnoreCase));

        if (mcp || stdio)
        {
            Environment.ExitCode = RunAgent(mcp).GetAwaiter().GetResult();
            return;
        }

        if (blueprints)
        {
            Environment.ExitCode = RunBlueprints();
            return;
        }

        if (acceptance)
        {
            Environment.ExitCode = RunAcceptance();
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

        if (generateOnly || headless || walkthrough)
        {
            // --generate-only: companions only (PNG skipped unless also --headless)
            // --headless: still PNG tour
            // --walkthrough: PNG frame sequence + ffmpeg MP4/GIF when available
            Environment.ExitCode = RunHeadless(
                exportPng: headless && !jsonOnly,
                walkthrough: walkthrough);
            return;
        }

        ApplicationHost = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddSingleton<CalypsoSession>();
                services.AddSingleton<CalypsoRenderer>();
                services.AddTransient<MainWindow>();
            })
            .Build();

        ApplicationHost.Start();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            ApplicationHost.StopAsync().GetAwaiter().GetResult();
        }
    }

    private static int RunBlueprints()
    {
        try
        {
            CalypsoCad.Generation.Blueprints.InternalsBlueprintEmitter.Emit();
            CalypsoCad.Generation.Blueprints.ManufacturerSheetEmitter.Emit();
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

        // Strip the acceptance probe so the user's generated ship is not left with a bow blob.
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

    /// <summary>Generate CAD companions and optionally export plan/orbit/interior PNGs or a walkthrough via hidden Raylib.</summary>
    private static int RunHeadless(bool exportPng, bool walkthrough)
    {
        var dir = CalypsoRevGGenerator.Generate();
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
                    Console.Error.WriteLine("Headless PNG export produced no frames (Raylib/GPU may be unavailable).");
                    return 2;
                }

                Console.WriteLine($"Headless Raylib PNG exports ({stills.Count}):");
                foreach (var path in stills)
                    Console.WriteLine($"  {path}");
            }

            if (walkthrough)
            {
                var wt = HeadlessWalkthroughExporter.Export(session, renderer, session.GeneratedDirectory);
                saved.AddRange(wt);
                if (wt.Count == 0)
                {
                    Console.Error.WriteLine("Walkthrough export produced no frames (Raylib/GPU may be unavailable).");
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

    /// <summary>
    /// Serves the Calypso CAD profile on stdio. WinExe still receives the redirected
    /// standard handles when an MCP client spawns the process.
    /// </summary>
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

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
