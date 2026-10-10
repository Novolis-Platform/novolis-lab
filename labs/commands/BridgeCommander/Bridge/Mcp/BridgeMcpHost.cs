using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BridgeCommander.Bridge.Mcp;

public static class BridgeMcpHost
{
    public static async Task RunStdioAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(BridgeMcpTools).Assembly);

        var app = builder.Build();
        _ = BridgeMcpRuntime.Session;
        await app.RunAsync().ConfigureAwait(false);
    }

    public static Task<int> RunMcpIntegrationTestAsync(CancellationToken cancellationToken = default) =>
        BridgeMcpIntegrationTest.RunAsync(cancellationToken);

    public static async Task<int> RunQaSmokeAsync(CancellationToken cancellationToken = default)
    {
        await using var session = BridgeSession.Create(BridgeSessionOptions.WithoutVoice);
        var failures = new List<string>();

        foreach (var name in BridgeQaScenarios.Names)
        {
            var report = await BridgeQaScenarios.RunAsync(session, name, cancellationToken)
                .ConfigureAwait(false);
            if (!report.Passed)
            {
                var failedSteps = report.Steps.Where(s => !s.Passed).Select(s => s.Prompt);
                failures.Add($"{name}: {string.Join("; ", failedSteps)}");
            }
        }

        if (failures.Count > 0)
        {
            await Console.Error.WriteLineAsync("QA smoke failed:").ConfigureAwait(false);
            foreach (var line in failures)
                await Console.Error.WriteLineAsync($"  - {line}").ConfigureAwait(false);
            return 1;
        }

        await Console.Out.WriteLineAsync($"QA smoke passed ({BridgeQaScenarios.Names.Count} scenarios).")
            .ConfigureAwait(false);
        return 0;
    }
}
