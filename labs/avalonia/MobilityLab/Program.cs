using System.Diagnostics;
using Avalonia;
using MobilityLab.Experiment;

namespace MobilityLab;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (TryParseHeadless(args, out var months))
        {
            RunHeadless(months, args);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    static void RunHeadless(int months, string[] args)
    {
        var single = args.Any(a => a.Equals("--single", StringComparison.OrdinalIgnoreCase));
        var war = args.Any(a => a.Equals("--war", StringComparison.OrdinalIgnoreCase));
        var sw = Stopwatch.StartNew();

        if (single)
        {
            var spec = ExperimentSpec.ShockDefault with
            {
                Months = months,
                WarShockOn = war,
            };
            if (args.Any(a => a.Equals("--static", StringComparison.OrdinalIgnoreCase)))
            {
                spec = ExperimentSpec.Default with { Months = months, WarShockOn = war };
            }

            var host = ExperimentHost.Run(spec);
            sw.Stop();
            Console.Write(MarkdownReport.Build(host.Result, host.Model, sw.Elapsed));
            return;
        }

        var study = StudySpec.Default with
        {
            Months = months,
            WarShockOn = war,
        };
        var battery = BatteryRunner.Run(study);
        sw.Stop();
        Console.Write(MarkdownReport.BuildBattery(battery, sw.Elapsed));
    }

    static bool TryParseHeadless(string[] args, out int months)
    {
        months = 48;
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--headless", StringComparison.OrdinalIgnoreCase))
                continue;
            if (i + 1 < args.Length && int.TryParse(args[i + 1], out var n) && n > 0)
                months = n;
            return true;
        }

        return false;
    }
}
