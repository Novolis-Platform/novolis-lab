using System.ComponentModel;
using ModelContextProtocol.Server;
using Novolis.Agent.Core;

namespace AvaloniaAgentMcp;

[McpServerToolType]
public static class SessionAgentMcpTools
{
    [McpServerTool]
    [Description("List known game-session hosts (HTTP marker, LocalIpc pipe, TCP).")]
    public static string SessionHosts() =>
        AvaloniaAgentRuntime.ToJson(new
        {
            hosts = SessionAgentRuntime.DiscoverHosts(),
            ipcOverride = SessionAgentRuntime.EndpointOverride,
            httpOverride = SessionAgentRuntime.HttpUrlOverride,
        });

    [McpServerTool]
    [Description("Connect via HTTP session surface (preferred). Empty uses marker / http://127.0.0.1:18765.")]
    public static async Task<string> SessionHttpConnect(
        [Description("Base URL, e.g. http://127.0.0.1:18765")]
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        await SessionAgentRuntime.SetHttpUrlAsync(baseUrl).ConfigureAwait(false);
        var hello = await SessionAgentRuntime.HelloAsync(cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(new
        {
            transport = "http",
            endpoint = SessionAgentRuntime.HttpUrlOverride
                        ?? SinsAgentSurfaceContract.Definition.TryReadHttpBaseUrl()
                        ?? $"(auto {SinsAgentSurfaceContract.Definition.DefaultHttpPort})",
            hello,
        });
    }

    [McpServerTool]
    [Description("Connect to game-session LocalIpc endpoint (default novolis-game-session-sins). Empty clears override.")]
    public static async Task<string> SessionConnect(
        [Description("Pipe/socket name. Omit or empty for auto/marker/sins default.")]
        string? endpoint = null,
        CancellationToken cancellationToken = default)
    {
        await SessionAgentRuntime.SetEndpointAsync(endpoint).ConfigureAwait(false);
        var hello = await SessionAgentRuntime.HelloAsync(cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(new
        {
            transport = "local-ipc",
            endpoint = SessionAgentRuntime.EndpointOverride ?? "(auto)",
            hello,
        });
    }

    [McpServerTool]
    [Description("Handshake session.hello — prefers HTTP marker, else LocalIpc.")]
    public static async Task<string> SessionHello(CancellationToken cancellationToken = default)
    {
        var response = await SessionAgentRuntime.HelloAsync(cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("session.snapshot — full player-facing decision-point state.")]
    public static async Task<string> SessionSnapshot(CancellationToken cancellationToken = default)
    {
        var response = await SessionAgentRuntime.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("session.actions — enabled/disabled decision actions.")]
    public static async Task<string> SessionActions(CancellationToken cancellationToken = default)
    {
        var response = await SessionAgentRuntime.ActionsAsync(cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("session.command — execute a typed action (travel, acceptSpot, continue, …).")]
    public static async Task<string> SessionCommand(
        [Description("Action id, e.g. travel, acceptSpot, continue, depart.")]
        string actionId,
        [Description("Optional travel destination system id.")]
        string? destSystemId = null,
        [Description("Optional board row index.")]
        int? index = null,
        [Description("Optional SKU for depart/market.")]
        string? sku = null,
        CancellationToken cancellationToken = default)
    {
        var response = await SessionAgentRuntime.CommandAsync(
            new AgentCommand { ActionId = actionId }
                .With(AgentCommandKeys.DestSystemId, destSystemId)
                .With(AgentCommandKeys.Index, index?.ToString())
                .With(AgentCommandKeys.Sku, sku),
            cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("session.continue — release day gate until next decision.")]
    public static async Task<string> SessionContinue(CancellationToken cancellationToken = default)
    {
        var response = await SessionAgentRuntime.ContinueAsync(cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Subscribe then poll until AwaitingDecision (or timeout). Prefers HTTP.")]
    public static async Task<string> SessionWaitDecision(
        [Description("Timeout in milliseconds (default 60000).")]
        int timeoutMs = 60000,
        [Description("Poll interval in milliseconds (default 400).")]
        int intervalMs = 400,
        CancellationToken cancellationToken = default)
    {
        await SessionAgentRuntime.SubscribeAsync(cancellationToken).ConfigureAwait(false);

        var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var events = SessionAgentRuntime.DrainEvents();
            var snapPoll = await SessionAgentRuntime.SnapshotAsync(cancellationToken).ConfigureAwait(false);
            var pause = snapPoll.GetType().GetProperty("PauseReason")?.GetValue(snapPoll)?.ToString()
                        ?? (snapPoll as AgentSnapshot)?.PauseReason;
            if (string.Equals(pause, "AwaitingDecision", StringComparison.OrdinalIgnoreCase))
            {
                return AvaloniaAgentRuntime.ToJson(new
                {
                    success = true,
                    timedOut = false,
                    via = "snapshot.poll",
                    snapshot = snapPoll,
                    events,
                });
            }

            await Task.Delay(Math.Max(50, intervalMs), cancellationToken).ConfigureAwait(false);
        }

        return AvaloniaAgentRuntime.ToJson(new
        {
            success = false,
            timedOut = true,
            events = SessionAgentRuntime.DrainEvents(),
        });
    }
}
