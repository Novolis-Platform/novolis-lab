using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArtillerySimulator;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{ProductIdentity.Title}: {ex.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        using var bootstrapLogFactory = LoggerFactory.Create(b =>
        {
            b.SetMinimumLevel(LogLevel.Information);
            b.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
            });
        });
        var bootstrapLog = bootstrapLogFactory.CreateLogger("ArtilleryCli");

        if (ArtilleryCli.TryHandleEarlyExit(args, bootstrapLog, out var earlyExit))
            return earlyExit;

        var options = ArtilleryCli.Parse(args);

        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(options.MinimumLogLevel);
        builder.Logging.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
            o.IncludeScopes = false;
        });
        builder.Services.AddArtillerySimulator(options);

        using var host = builder.Build();
        var log = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
        log.LogInformation(
            "{Title} host starting (level={LevelId}, verbose={Verbose})",
            ProductIdentity.Title,
            options.Level.Id,
            options.Verbose);

        var exit = host.Services.GetRequiredService<ArtilleryGameRunner>().Run();

        log.LogInformation("{Title} host exiting with code {ExitCode}", ProductIdentity.Title, exit);
        return exit;
    }
}
