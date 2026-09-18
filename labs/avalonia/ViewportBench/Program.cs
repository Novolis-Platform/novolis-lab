using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Win32;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Avalonia.ThreeD;
using Novolis.Avalonia.ThreeD.Services;
using Novolis.Avalonia.ThreeD.Session;
using Novolis.Avalonia.ThreeD.Ui;
using Novolis.ThreeD;

namespace ViewportBench;

internal static class Program
{
    internal static IHost ApplicationHost { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        ApplicationHost = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddSingleton(_ => new SceneSessionService(ResolveDocument(args)) { AppId = "viewportbench" });
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

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Wgl] })
            .LogToTrace();

    private static SceneDocument ResolveDocument(string[] args)
    {
        if (Has(args, "--lights", "--look"))
            return SceneDocument.CreateLookSetup();
        if (Has(args, "--edit"))
            return SceneDocument.CreateEditBox();
        if (Has(args, "--array", "--cloner"))
            return SceneDocument.CreateClonerRow();
        if (Has(args, "--boolean", "--boole"))
            return SceneDocument.CreateBooleCut();
        if (Has(args, "--sample", "--keel"))
            return LoadKeelOrGallery();
        return SceneDocument.CreatePrimitiveGallery();
    }

    private static bool Has(string[] args, params string[] flags) =>
        flags.Any(f => args.Any(a => a.Equals(f, StringComparison.OrdinalIgnoreCase)));

    private static SceneDocument LoadKeelOrGallery()
    {
        foreach (var candidate in KeelCandidates())
        {
            if (File.Exists(candidate))
                return SceneSerializer.Load(candidate);
        }

        return SceneDocument.CreatePrimitiveGallery();
    }

    private static IEnumerable<string> KeelCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "samples", "keel-transport.nov3djson");
        yield return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "SceneLab", "samples", "keel-transport.nov3djson"));
        yield return @"D:\novolis\novolis-lab\labs\avalonia\SceneLab\samples\keel-transport.nov3djson";
    }
}

internal sealed class MainWindow : Window
{
    private readonly SceneSessionService _session;
    private readonly SceneViewportCamera _sharedCamera;
    private readonly SceneViewportControl _gl;
    private readonly SceneViewportControl _cpu;
    private readonly SceneViewportControl _vk;
    private readonly SceneViewportControl _rl;
    private readonly TextBlock _hud;
    private readonly TextBlock _status;
    private readonly DispatcherTimer _hudTimer;
    private readonly DispatcherTimer _autoOrbitTimer;
    private bool _autoOrbit;

    public MainWindow(SceneSessionService session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Title = "ViewportBench — shared scene + camera across OpenGL / CPU / Vulkan / Raylib";
        Width = 1760;
        Height = 1040;
        MinWidth = 1100;
        MinHeight = 700;
        Background = new SolidColorBrush(Color.FromRgb(12, 16, 22));

        // Free orbit for all panes; document ActiveCamera must not fight mouse nav.
        _session.Document.ActiveCameraId = null;
        _sharedCamera = new SceneViewportCamera(_session) { FollowDocumentCamera = false };

        _gl = new SceneViewportControl(_session, SceneViewportBackendKind.OpenGl, _sharedCamera);
        _cpu = new SceneViewportControl(_session, SceneViewportBackendKind.Cpu, _sharedCamera);
        _vk = new SceneViewportControl(_session, SceneViewportBackendKind.Vulkan, _sharedCamera);
        _rl = new SceneViewportControl(_session, SceneViewportBackendKind.Raylib, _sharedCamera);

        _sharedCamera.Changed += () =>
        {
            // Cheap kicks only — Vulkan/CPU self-pace on timers; never sync GPU+readback here.
            _gl.RequestPresent();
            _cpu.RequestPresent();
            _vk.RequestPresent();
            _rl.RequestPresent();
        };

        _hud = new TextBlock
        {
            FontFamily = new FontFamily("Consolas,Cascadia Mono,Courier New"),
            FontSize = 12.5,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 220, 210)),
            Margin = new Thickness(12, 8),
            TextWrapping = TextWrapping.Wrap,
        };

        _status = new TextBlock
        {
            Margin = new Thickness(12, 0, 12, 8),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(160, 175, 190)),
            Text = "Orbit: LMB drag or MMB / Alt+LMB · Zoom: wheel · Fit resets all panes. Stats update ~4 Hz; orbit columns fill while camera moves.",
        };

        Content = BuildLayout();

        _hudTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => RefreshHud());
        // ~30 Hz orbit input — Vulkan readback cannot keep up with 60 Hz sync presents on the UI thread.
        _autoOrbitTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Background, (_, _) =>
        {
            if (!_autoOrbit)
                return;
            _sharedCamera.OrbitDrag(2.2f, 0.35f);
        });

        Opened += (_, _) =>
        {
            // Start GPU hosts before Raylib — GLFW WGL can starve Avalonia OpenGlControlBase init.
            _gl.Start();
            _cpu.Start();
            _vk.Start();
            _hudTimer.Start();
            RefreshHud();
            DispatcherTimer.RunOnce(() =>
            {
                _rl.Start();
                RefreshHud();
            }, TimeSpan.FromMilliseconds(400));
        };
        Closed += (_, _) =>
        {
            _autoOrbitTimer.Stop();
            _hudTimer.Stop();
            _gl.Stop();
            _cpu.Stop();
            _vk.Stop();
            _rl.Stop();
        };
    }

    private Control BuildLayout()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("*,*"),
            Margin = new Thickness(6),
        };
        var panes = new (string Title, SceneViewportControl Vp)[]
        {
            ("OpenGL ★ CAD default", _gl),
            ("CPU RGBA (fallback)", _cpu),
            ("Vulkan wire (bench)", _vk),
            ("Raylib stream (legacy)", _rl),
        };
        for (var i = 0; i < panes.Length; i++)
        {
            var wrap = WrapPane(panes[i].Title, panes[i].Vp);
            Grid.SetColumn(wrap, i % 2);
            Grid.SetRow(wrap, i / 2);
            grid.Children.Add(wrap);
        }

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(12, 10, 12, 4),
            Children =
            {
                ToolButton("Fit", () => _sharedCamera.Fit()),
                ToolButton("Reset meters", ResetMeters),
                ToolButton("Gallery", () => SwapDoc(SceneDocument.CreatePrimitiveGallery())),
                ToolButton("Lights", () => SwapDoc(SceneDocument.CreateLookSetup())),
                ToolButton("Edit box", () => SwapDoc(SceneDocument.CreateEditBox())),
                ToolButton("Keel / sample", () => SwapDoc(LoadSample())),
                ToolToggle("Auto-orbit stress", on =>
                {
                    _autoOrbit = on;
                    if (on) _autoOrbitTimer.Start();
                    else _autoOrbitTimer.Stop();
                }),
            },
        };

        var header = new DockPanel
        {
            [DockPanel.DockProperty] = Dock.Top,
            Children =
            {
                new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(18, 28, 36)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(40, 70, 85)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "ViewportBench",
                                FontSize = 20,
                                FontWeight = FontWeight.SemiBold,
                                Margin = new Thickness(12, 10, 12, 0),
                                Foreground = new SolidColorBrush(Color.FromRgb(230, 240, 245)),
                            },
                            new TextBlock
                            {
                                Text = $"OpenGL is the CAD / 3D choice · shared nav · scene={_session.Document.Name}",
                                Margin = new Thickness(12, 2, 12, 6),
                                FontSize = 12,
                                Foreground = new SolidColorBrush(Color.FromRgb(150, 170, 185)),
                            },
                            toolbar,
                            _hud,
                            _status,
                        },
                    },
                },
            },
        };

        return new DockPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(12, 16, 22)),
            Children = { header, grid },
        };
    }

    private void SwapDoc(SceneDocument doc)
    {
        _session.ReplaceDocument(doc);
        _session.Document.ActiveCameraId = null;
        _sharedCamera.Fit();
        ResetMeters();
        _status.Text = $"Loaded {doc.Name}. Orbit any pane — all four share the camera.";
    }

    private SceneDocument LoadSample()
    {
        foreach (var c in ProgramKeel())
        {
            if (File.Exists(c))
                return SceneSerializer.Load(c);
        }

        return SceneDocument.CreatePrimitiveGallery();
    }

    private static IEnumerable<string> ProgramKeel()
    {
        yield return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "SceneLab", "samples", "keel-transport.nov3djson"));
        yield return @"D:\novolis\novolis-lab\labs\avalonia\SceneLab\samples\keel-transport.nov3djson";
    }

    private void ResetMeters()
    {
        _gl.FrameMeter.Reset();
        _cpu.FrameMeter.Reset();
        _vk.FrameMeter.Reset();
        _rl.FrameMeter.Reset();
        RefreshHud();
    }

    private void RefreshHud()
    {
        var moving = _sharedCamera.CameraInteracting || _autoOrbit;
        var errors = new List<string>();
        if (_gl.LastError is { Length: > 0 } ge) errors.Add($"GL: {ge}");
        if (_vk.LastError is { Length: > 0 } ve) errors.Add($"VK: {Truncate(ve, 120)}");

        _hud.Text =
            $"{_gl.FrameMeter.FormatLine("OpenGL")}\n" +
            $"{_cpu.FrameMeter.FormatLine("CPU   ")}\n" +
            $"{_vk.FrameMeter.FormatLine("Vulkan")}\n" +
            $"{_rl.FrameMeter.FormatLine("Raylib")}\n" +
            $"camera moving: {(moving ? "yes" : "no")}  ·  meshes={_session.Evaluator.Cache.EvaluatedMeshes.Count}" +
            $"  lights={_session.Evaluator.Cache.Lights.Count}" +
            (errors.Count > 0 ? "\n" + string.Join("\n", errors) : string.Empty);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private static Control WrapPane(string title, Control child) =>
        new Border
        {
            Margin = new Thickness(4),
            BorderBrush = new SolidColorBrush(Color.FromRgb(36, 55, 68)),
            BorderThickness = new Thickness(1),
            Child = new DockPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        Margin = new Thickness(8, 6),
                        FontSize = 13,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(180, 200, 210)),
                        [DockPanel.DockProperty] = Dock.Top,
                    },
                    child,
                },
            },
        };

    private static Button ToolButton(string label, Action onClick)
    {
        var b = new Button
        {
            Content = label,
            Padding = new Thickness(10, 6),
            Background = new SolidColorBrush(Color.FromRgb(28, 48, 58)),
            Foreground = Brushes.WhiteSmoke,
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    private static ToggleButton ToolToggle(string label, Action<bool> onToggle)
    {
        var b = new ToggleButton
        {
            Content = label,
            Padding = new Thickness(10, 6),
            Background = new SolidColorBrush(Color.FromRgb(28, 48, 58)),
            Foreground = Brushes.WhiteSmoke,
        };
        b.IsCheckedChanged += (_, _) => onToggle(b.IsChecked == true);
        return b;
    }
}
