using Avalonia;

namespace AssetStudioLab;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Any(a => a is "--headless" or "--generate-only"))
        {
            var report = AssetStudioHarness.RunAsync().GetAwaiter().GetResult();
            Console.WriteLine(report.Success ? "Asset Studio headless: ok" : "Asset Studio headless: FAILED");
            Console.WriteLine(report.Stack);
            Console.WriteLine($"IR nodes: {report.Ir.Definitions.Sum(d => d.Nodes.Count)}");
            Console.WriteLine($"Beam luminance: {report.BeamLuminance.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}");
            Console.WriteLine($"Pipeline exit: {report.PipelineExit}");
            Environment.Exit(report.Success ? 0 : 1);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
