using Avalonia;
using CapitalistSimulator.Cli;
using CapitalistSimulator.Persistence;
using CapitalistSimulator.Sim;
using CapitalistSimulator.Ui;

namespace CapitalistSimulator;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            var options = RunOptions.Parse(args);
            if (options.PlaytestWin is { } playtest)
            {
                return playtest.ToLowerInvariant() switch
                {
                    "retail" => WinPlaytest.RunRetailProfit(options.Seed),
                    "wine" => WinPlaytest.RunWineDominance(options.Seed),
                    "both" => Math.Max(WinPlaytest.RunRetailProfit(options.Seed), WinPlaytest.RunWineDominance(options.Seed + 1)),
                    _ => throw new ArgumentException("Use --playtest-win retail|wine|both"),
                };
            }

            if (options.Mode == AppMode.Headless)
                return HeadlessRunner.Run(options);

            GameWorld world;
            var store = new SaveStore();
            if (options.LoadName is { } load)
                world = store.Load(load) ?? WorldFactory.Create(options.Scenario, options.StartingCash, options.AiCount, options.AiAggressiveness, options.Seed);
            else
                world = WorldFactory.Create(options.Scenario, options.StartingCash, options.AiCount, options.AiAggressiveness, options.Seed);

            App.Configure(options, world);
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
