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

internal sealed class MainWindow : Window
{
    private readonly SceneSessionService _session;
    private readonly SceneArtifactDumper _artifacts;
    private SceneEditorSurface? _surface;
    private bool _dumpBusy;

    public MainWindow(SceneSessionService session)
    {
        _session = session;
        _artifacts = new SceneArtifactDumper(
            session,
            AppContext.BaseDirectory);

        Title = Program.CompareBackends
            ? "SceneLab - renderer compare (OpenGL | CPU | Vulkan | Raylib)"
            : $"SceneLab - {Program.ViewportBackend}";
        Width = Program.CompareBackends ? 1800 : 1600;
        Height = Program.CompareBackends ? 1000 : 920;
        MinWidth = 1100;
        MinHeight = 640;
        Background = new SolidColorBrush(Color.FromRgb(14, 20, 28));

        if (Program.CompareBackends)
        {
            Content = BuildCompareLayout(session);
            return;
        }

        var surface = new SceneEditorSurface(session, composeDefaultLayout: false, backend: Program.ViewportBackend);
        _surface = surface;
        Content = BuildEditorLayout(surface);
        session.DumpArtifactsRequested += payload => _ = OnDumpAsync(payload);
        session.DocumentChanged += RefreshTitle;
        RefreshTitle();
        KeyDown += OnFileHotkeys;
        Opened += (_, _) => surface.StartPresenting();
        Closed += (_, _) => surface.StopPresenting();
    }

    private void OnFileHotkeys(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (_surface is null)
            return;
        var ctrl = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control);
        if (!ctrl)
            return;

        void Notice(string m) => _surface!.StatusBar.SetNotice(m);

        if (e.Key == Avalonia.Input.Key.O)
        {
            SceneFileActions.Open(_surface, _session, Notice);
            e.Handled = true;
        }
        else if (e.Key == Avalonia.Input.Key.I)
        {
            SceneFileActions.ImportMesh(_surface, _session, Notice);
            e.Handled = true;
        }
        else if (e.Key == Avalonia.Input.Key.S && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
        {
            SceneFileActions.SaveAs(_surface, _session, Notice);
            e.Handled = true;
        }
        else if (e.Key == Avalonia.Input.Key.S)
        {
            SceneFileActions.Save(_surface, _session, Notice);
            e.Handled = true;
        }
    }

    private void RefreshTitle()
    {
        var path = _session.DocumentPath;
        var name = string.IsNullOrWhiteSpace(path)
            ? _session.Document.Name
            : Path.GetFileName(path);
        Title = Program.CompareBackends
            ? $"SceneLab - renderer compare · {name}"
            : $"SceneLab - {Program.ViewportBackend} · {name}";
    }

    private async Task OnDumpAsync(string payload)
    {
        if (_dumpBusy || _surface is null)
            return;
        _dumpBusy = true;
        try
        {
            var kind = payload;
            var root = _artifacts.DataRoot;
            var exactDumpsDir = false;
            var pipe = payload.IndexOf('|');
            if (pipe >= 0)
            {
                kind = payload[..pipe];
                var overrideRoot = payload[(pipe + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(overrideRoot))
                {
                    root = overrideRoot;
                    // UI folder picker passes the destination directory itself (not a data root).
                    exactDumpsDir = true;
                }
            }

            var dumper = !exactDumpsDir && string.Equals(root, _artifacts.DataRoot, StringComparison.OrdinalIgnoreCase)
                ? _artifacts
                : new SceneArtifactDumper(_session, root, dataRootIsDumpsDirectory: exactDumpsDir);

            var result = await dumper.DumpAsync(kind, this, _surface.Viewport).ConfigureAwait(true);
            Title = $"SceneLab - {Program.ViewportBackend} · dumped";
            _surface.StatusBar.SetNotice($"dumped {result.Kind} → {result.ManifestPath}");
            if (exactDumpsDir)
                SceneFileActions.LastDumpDirectory = root;
        }
        finally
        {
            _dumpBusy = false;
        }
    }

    private Control BuildCompareLayout(SceneSessionService session)
    {
        session.Document.ActiveCameraId = null;
        var shared = new SceneViewportCamera(session) { FollowDocumentCamera = false };
        var gl = new SceneViewportControl(session, SceneViewportBackendKind.OpenGl, shared);
        var cpu = new SceneViewportControl(session, SceneViewportBackendKind.Cpu, shared);
        var vk = new SceneViewportControl(session, SceneViewportBackendKind.Vulkan, shared);
        var rl = new SceneViewportControl(session, SceneViewportBackendKind.Raylib, shared);
        shared.Changed += () =>
        {
            gl.RequestPresent();
            cpu.RequestPresent();
            vk.RequestPresent();
            rl.RequestPresent();
        };

        var w0 = Wrap("OpenGL (Silk)", gl);
        var w1 = Wrap("CPU RGBA", cpu);
        var w2 = Wrap("Vulkan wire", vk);
        var w3 = Wrap("Raylib stream", rl);
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("*,*"),
            Margin = new Thickness(6),
        };
        Grid.SetColumn(w0, 0); Grid.SetRow(w0, 0);
        Grid.SetColumn(w1, 1); Grid.SetRow(w1, 0);
        Grid.SetColumn(w2, 0); Grid.SetRow(w2, 1);
        Grid.SetColumn(w3, 1); Grid.SetRow(w3, 1);
        grid.Children.Add(w0);
        grid.Children.Add(w1);
        grid.Children.Add(w2);
        grid.Children.Add(w3);

        Opened += (_, _) =>
        {
            gl.Start();
            cpu.Start();
            vk.Start();
            rl.Start();
        };
        Closed += (_, _) =>
        {
            gl.Stop();
            cpu.Stop();
            vk.Stop();
            rl.Stop();
        };

        return new DockPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = "OpenGL is the CAD presenter. Other panes are compare-only (Vulkan wire = graphics + CPU readback; path-trace is separate).",
                    Margin = new Thickness(10, 8),
                    Foreground = Brushes.WhiteSmoke,
                    [DockPanel.DockProperty] = Dock.Top,
                },
                grid,
            },
        };
    }

    private static Control Wrap(string title, Control child) =>
        new DockPanel
        {
            Margin = new Thickness(4),
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    Margin = new Thickness(4),
                    FontSize = 13,
                    Foreground = Brushes.LightGray,
                    [DockPanel.DockProperty] = Dock.Top,
                },
                child,
            },
        };

    private Control BuildEditorLayout(SceneEditorSurface surface)
    {
        var rightRail = new ScrollViewer
        {
            Width = 300,
            Content = new StackPanel
            {
                Children =
                {
                    surface.MeshAttributes,
                    surface.ModifierStack,
                    surface.Properties,
                },
            },
        };

        var center = new Grid { ColumnDefinitions = new ColumnDefinitions("260,*,300") };
        Grid.SetColumn(surface.ObjectManager, 0);
        Grid.SetColumn(surface.Viewport, 1);
        Grid.SetColumn(rightRail, 2);
        center.Children.Add(surface.ObjectManager);
        center.Children.Add(surface.Viewport);
        center.Children.Add(rightRail);

        var sessionLine = new TextBlock
        {
            Margin = new Thickness(10, 2),
            FontSize = 11,
            Opacity = 0.75,
            Foreground = Brushes.WhiteSmoke,
            Text = Program.SceneSurface?.HttpBaseUrl is { } url
                ? $"Session HTTP {url}  TCP :{Program.SceneSurface.TcpPort}  renderer={Program.ViewportBackend}"
                : $"Session off · renderer={Program.ViewportBackend}",
        };

        var bottom = new StackPanel
        {
            [DockPanel.DockProperty] = Dock.Bottom,
            Children = { surface.StatusBar, sessionLine },
        };

        var chrome = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(22, 32, 42)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(40, 60, 75)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = surface.CreateChrome(_artifacts.DumpsDirectory),
            [DockPanel.DockProperty] = Dock.Top,
        };

        return new DockPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(14, 20, 28)),
            Children = { chrome, bottom, center },
        };
    }
}
