using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AvaloniaAgentMcp;

public static class AvaloniaAgentMcpHost
{
    public static async Task RunStdioAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(AvaloniaAgentMcpTools).Assembly);

        var app = builder.Build();
        await app.RunAsync().ConfigureAwait(false);
    }
}
