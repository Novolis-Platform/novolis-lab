using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MauiAgentMcp;

public static class MauiAgentMcpHost
{
    public static async Task RunStdioAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(MauiAgentMcpTools).Assembly);

        var app = builder.Build();
        await app.RunAsync().ConfigureAwait(false);
    }
}
