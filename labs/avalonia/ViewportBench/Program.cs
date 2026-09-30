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
