using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Win32;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Agent.Surface;
using Novolis.Avalonia.ThreeD;
using Novolis.Avalonia.ThreeD.Services;
using Novolis.Avalonia.ThreeD.Session;
using Novolis.Avalonia.ThreeD.Ui;
using Novolis.ThreeD;

namespace SceneLab;

internal static class Program
{
    internal static IHost ApplicationHost { get; private set; } = null!;
    internal static AgentSurface? SceneSurface { get; private set; }
    internal static SceneViewportBackendKind ViewportBackend { get; private set; } = SceneViewportBackendKind.OpenGl;
    internal static bool CompareBackends { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--spatial-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = SpatialSmoke.Run();
            return;
        }

        ViewportBackend = ParseBackend(args);
        CompareBackends = args.Any(a => a.Equals("--compare", StringComparison.OrdinalIgnoreCase));

        ApplicationHost = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddSingleton(_ =>
                {
                    var (doc, path) = ResolveStartup(args);
                    var session = new SceneSessionService();
                    session.ReplaceDocument(doc, path);
                    session.AppId = "scenelab";
                    return session;
                });
                services.AddTransient<MainWindow>();
            })
            .Build();

        ApplicationHost.Start();
        try
        {
            var session = ApplicationHost.Services.GetRequiredService<SceneSessionService>();
            SceneSurface = AgentSurface.AttachAll(session, session.Definition)
                           ?? AgentSurface.TryAttachFromEnvironment(session, session.Definition);
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            if (SceneSurface is not null)
                SceneSurface.DisposeAsync().AsTask().GetAwaiter().GetResult();
            ApplicationHost.StopAsync().GetAwaiter().GetResult();
        }
    }

    private static SceneViewportBackendKind ParseBackend(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.Equals("--renderer", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return MapBackend(args[++i]);
            if (a.StartsWith("--renderer=", StringComparison.OrdinalIgnoreCase))
                return MapBackend(a["--renderer=".Length..]);
            if (a.Equals("--opengl", StringComparison.OrdinalIgnoreCase) || a.Equals("--gl", StringComparison.OrdinalIgnoreCase))
                return SceneViewportBackendKind.OpenGl;
            if (a.Equals("--cpu", StringComparison.OrdinalIgnoreCase))
                return SceneViewportBackendKind.Cpu;
            if (a.Equals("--raylib", StringComparison.OrdinalIgnoreCase))
                return SceneViewportBackendKind.Raylib;
            if (a.Equals("--vulkan", StringComparison.OrdinalIgnoreCase) || a.Equals("--vk", StringComparison.OrdinalIgnoreCase))
                return SceneViewportBackendKind.Vulkan;
        }

        var env = Environment.GetEnvironmentVariable("SCENELAB_RENDERER");
        return string.IsNullOrWhiteSpace(env) ? SceneViewportBackendKind.OpenGl : MapBackend(env);
    }

    private static SceneViewportBackendKind MapBackend(string raw) =>
        raw.Trim().ToLowerInvariant() switch
        {
            "cpu" or "rgba" or "software" => SceneViewportBackendKind.Cpu,
            "raylib" or "rl" => SceneViewportBackendKind.Raylib,
            "vulkan" or "vk" => SceneViewportBackendKind.Vulkan,
            _ => SceneViewportBackendKind.OpenGl,
        };

    private static (SceneDocument Doc, string? Path) ResolveStartup(string[] args)
    {
        if (Has(args, "--array", "--cloner"))
            return (SceneDocument.CreateClonerRow(), null);
        if (Has(args, "--boolean", "--boole"))
            return (SceneDocument.CreateBooleCut(), null);
        if (Has(args, "--lights", "--look"))
            return (SceneDocument.CreateLookSetup(), null);
        if (Has(args, "--edit"))
            return (SceneDocument.CreateEditBox(), null);
        if (Has(args, "--gallery"))
            return (SceneDocument.CreatePrimitiveGallery(), null);
        if (Has(args, "--sample", "--keel", "--corvette") || CompareBackends)
            return LoadDemoSampleOrFallback();
        return (SceneDocument.CreatePrimitiveStage("Untitled"), null);
    }

    private static bool Has(string[] args, params string[] flags) =>
        flags.Any(f => args.Any(a => a.Equals(f, StringComparison.OrdinalIgnoreCase)));

    private static (SceneDocument Doc, string? Path) LoadDemoSampleOrFallback()
    {
        foreach (var candidate in DemoSampleCandidates())
        {
            if (File.Exists(candidate))
                return (SceneSerializer.Load(candidate), candidate);
        }

        return (SceneDocument.CreatePrimitiveStage("Untitled"), null);
    }

    private static IEnumerable<string> DemoSampleCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "samples", "keel-transport.nov3djson");
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples", "keel-transport.nov3djson"));
        yield return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "apps", "avalonia", "SceneLab", "samples", "keel-transport.nov3djson"));
        var cwd = Directory.GetCurrentDirectory();
        yield return Path.Combine(cwd, "apps", "avalonia", "SceneLab", "samples", "keel-transport.nov3djson");
        yield return Path.Combine(cwd, "samples", "keel-transport.nov3djson");
        yield return @"D:\novolis\novolis-lab\labs\avalonia\SceneLab\samples\keel-transport.nov3djson";
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Wgl] })
            .LogToTrace(Avalonia.Logging.LogEventLevel.Warning);
}
