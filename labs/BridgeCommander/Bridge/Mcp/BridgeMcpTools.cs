using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace BridgeCommander.Bridge.Mcp;

[McpServerToolType]
public static class BridgeMcpTools
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerTool]
    [Description("Returns the current bridge snapshot: heading, warp, shields, target lock, status line, and recent command log.")]
    public static string GetBridgeSnapshot() =>
        JsonSerializer.Serialize(BridgeMcpRuntime.Session.GetSnapshot(), JsonOptions);

    [McpServerTool]
    [Description("Transmits a bridge order (same as the TUI Transmit button). Waits until the command queue is idle unless waitForIdle is false.")]
    public static async Task<string> TransmitOrder(
        [Description("Full order text, e.g. 'helm heading 270' or 'belay that'.")]
        string prompt,
        [Description("When true (default), waits until queued commands finish executing.")]
        bool waitForIdle = true,
        CancellationToken cancellationToken = default)
    {
        var result = await BridgeMcpRuntime.Session
            .TransmitAsync(prompt, waitForIdle, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Resets the bridge to the initial duty-shift state without restarting the process.")]
    public static string ResetBridge()
    {
        BridgeMcpRuntime.ResetSession();
        return JsonSerializer.Serialize(BridgeMcpRuntime.Session.GetSnapshot(), JsonOptions);
    }

    [McpServerTool]
    [Description("Runs a built-in QA scenario and returns a pass/fail report with per-step status.")]
    public static async Task<string> RunQaScenario(
        [Description("Scenario name: natural-orders, kr12-engagement, belay-interrupt, clear-queue, parse-failures.")]
        string scenario,
        CancellationToken cancellationToken = default)
    {
        var report = await BridgeQaScenarios
            .RunAsync(BridgeMcpRuntime.Session, scenario, cancellationToken)
            .ConfigureAwait(false);
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    [McpServerTool]
    [Description("Lists available QA scenario names for RunQaScenario.")]
    public static string ListQaScenarios() =>
        JsonSerializer.Serialize(BridgeQaScenarios.Names, JsonOptions);
}
