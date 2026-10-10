using System.Text.Json;

namespace BridgeCommander.Bridge.Mcp;

/// <summary>
/// Exercises every MCP tool path (same methods the stdio server exposes).
/// </summary>
public static class BridgeMcpIntegrationTest
{
    private static int _passed;
    private static int _failed;
    private static readonly List<string> Failures = [];

    public static async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        _passed = 0;
        _failed = 0;
        Failures.Clear();

        await BridgeMcpRuntime.ReplaceSessionAsync().ConfigureAwait(false);

        await TestListQaScenarios(cancellationToken).ConfigureAwait(false);
        await TestGetBridgeSnapshotInitial(cancellationToken).ConfigureAwait(false);
        await TestTransmitHelmHeading(cancellationToken).ConfigureAwait(false);
        await TestTransmitParseFailure(cancellationToken).ConfigureAwait(false);
        await TestTransmitSuggestions(cancellationToken).ConfigureAwait(false);
        await TestTransmit3dMarkHeading(cancellationToken).ConfigureAwait(false);
        await TestTransmitNorwegianDecimals(cancellationToken).ConfigureAwait(false);
        await TestTransmitHelp(cancellationToken).ConfigureAwait(false);
        await TestTransmitFireWithoutLock(cancellationToken).ConfigureAwait(false);
        await TestResetBridge(cancellationToken).ConfigureAwait(false);
        await TestAllQaScenarios(cancellationToken).ConfigureAwait(false);
        await TestUnknownScenario(cancellationToken).ConfigureAwait(false);
        await TestRapidQueue(cancellationToken).ConfigureAwait(false);
        await TestClearQueueViaMcp(cancellationToken).ConfigureAwait(false);
        await TestBelayViaMcp(cancellationToken).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"MCP integration: {_passed} passed, {_failed} failed.")
            .ConfigureAwait(false);
        foreach (var failure in Failures)
            await Console.Error.WriteLineAsync($"  FAIL: {failure}").ConfigureAwait(false);

        return _failed == 0 ? 0 : 1;
    }

    private static async Task TestListQaScenarios(CancellationToken cancellationToken)
    {
        var json = BridgeMcpTools.ListQaScenarios();
        var names = JsonSerializer.Deserialize<string[]>(json);
        Assert(names is { Length: 5 }, "ListQaScenarios returns 5 scenarios");
        Assert(names!.Contains("natural-orders"), "includes natural-orders");
        await Task.CompletedTask;
    }

    private static async Task TestGetBridgeSnapshotInitial(CancellationToken cancellationToken)
    {
        var snap = DeserializeSnapshot(BridgeMcpTools.GetBridgeSnapshot());
        Assert(snap.Heading == 180, "initial heading 180");
        Assert(snap.SpeedWarp == 5, "initial warp 5");
        Assert(snap.IsIdle, "initial idle");
        await Task.CompletedTask;
    }

    private static async Task TestTransmitHelmHeading(CancellationToken cancellationToken)
    {
        var json = await BridgeMcpTools.TransmitOrder("helm heading 270", true, cancellationToken)
            .ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TransmitResult>(json);
        Assert(result?.ParseSucceeded == true, "helm heading 270 parses");
        Assert(result?.Snapshot.Heading == 270, "heading set to 270");
        Assert(result?.Snapshot.IsIdle == true, "idle after helm heading");
    }

    private static async Task TestTransmitParseFailure(CancellationToken cancellationToken)
    {
        var json = await BridgeMcpTools.TransmitOrder("heading 270", true, cancellationToken)
            .ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TransmitResult>(json);
        Assert(result?.ParseSucceeded == false, "missing prefix fails");
        Assert(
            result?.StatusLine.Contains("prefix", StringComparison.OrdinalIgnoreCase) == true,
            "unknown context message");
    }

    private static async Task TestTransmitSuggestions(CancellationToken cancellationToken)
    {
        var json = await BridgeMcpTools.TransmitOrder("helm ful stop", true, cancellationToken)
            .ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TransmitResult>(json);
        Assert(result?.ParseSucceeded == false, "typo fails parse");
        Assert(
            result?.NewHistoryLines.Any(l => l.Contains("Did you mean", StringComparison.OrdinalIgnoreCase)) == true,
            "suggestions in history");
    }

    private static async Task TestTransmit3dMarkHeading(CancellationToken cancellationToken)
    {
        var json = await BridgeMcpTools
            .TransmitOrder("helm set heading to 122 mark 6 by 180 mark 2", true, cancellationToken)
            .ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TransmitResult>(json);
        Assert(result?.ParseSucceeded == true, "mark heading parses");
        Assert(Math.Abs(result!.Snapshot.Heading - 122.6) < 0.01, "heading 122.6");
        Assert(Math.Abs(result.Snapshot.HeadingBy - 180.2) < 0.01, "by 180.2");
    }

    private static async Task TestTransmitNorwegianDecimals(CancellationToken cancellationToken)
    {
        var json = await BridgeMcpTools
            .TransmitOrder("helm set course 123,5 by 119,4", true, cancellationToken)
            .ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TransmitResult>(json);
        Assert(Math.Abs(result!.Snapshot.Heading - 123.5) < 0.01, "comma decimal heading");
        Assert(Math.Abs(result.Snapshot.HeadingBy - 119.4) < 0.01, "comma decimal by");
    }

    private static async Task TestTransmitHelp(CancellationToken cancellationToken)
    {
        var json = await BridgeMcpTools.TransmitOrder("help tactical", true, cancellationToken)
            .ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TransmitResult>(json);
        Assert(result?.ParseSucceeded == true, "help parses");
        Assert(
            result?.Snapshot.HelpLines.Any(l => l.Contains("Tactical", StringComparison.OrdinalIgnoreCase)) == true,
            "help tactical filters reference");
    }

    private static async Task TestTransmitFireWithoutLock(CancellationToken cancellationToken)
    {
        BridgeMcpRuntime.ResetSession();
        var json = await BridgeMcpTools.TransmitOrder("tactical fire", true, cancellationToken)
            .ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TransmitResult>(json);
        Assert(result?.ParseSucceeded == true, "fire parses and queues");
        Assert(
            result?.StatusLine.Contains("no target lock", StringComparison.OrdinalIgnoreCase) == true,
            "fire without lock rejected at execution");
        Assert(result?.Snapshot.LastExecutedCommand is null, "no last command on failed fire");
    }

    private static Task TestResetBridge(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var json = BridgeMcpTools.ResetBridge();
        var snap = DeserializeSnapshot(json);
        Assert(snap.Heading == 180, "reset heading");
        Assert(snap.TargetLocked == false, "reset target");
        return Task.CompletedTask;
    }

    private static async Task TestAllQaScenarios(CancellationToken cancellationToken)
    {
        foreach (var name in BridgeQaScenarios.Names)
        {
            var json = await BridgeMcpTools.RunQaScenario(name, cancellationToken).ConfigureAwait(false);
            var report = JsonSerializer.Deserialize<BridgeQaReport>(json);
            Assert(report?.Passed == true, $"scenario {name} passes");
        }
    }

    private static async Task TestUnknownScenario(CancellationToken cancellationToken)
    {
        try
        {
            await BridgeMcpTools.RunQaScenario("nonexistent-scenario", cancellationToken)
                .ConfigureAwait(false);
            Assert(false, "unknown scenario should throw");
        }
        catch (ArgumentException)
        {
            Assert(true, "unknown scenario throws ArgumentException");
        }
    }

    private static async Task TestRapidQueue(CancellationToken cancellationToken)
    {
        BridgeMcpRuntime.ResetSession();
        _ = BridgeMcpTools.TransmitOrder("helm warp 3", false, cancellationToken);
        _ = BridgeMcpTools.TransmitOrder("helm warp 5", false, cancellationToken);
        var json = await BridgeMcpTools.TransmitOrder("helm warp 7", true, cancellationToken)
            .ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TransmitResult>(json);
        Assert(result?.Snapshot.SpeedWarp == 7, "final warp after queue drain");
    }

    private static async Task TestClearQueueViaMcp(CancellationToken cancellationToken)
    {
        BridgeMcpRuntime.ResetSession();
        _ = BridgeMcpTools.TransmitOrder("helm warp 8", false, cancellationToken);
        _ = BridgeMcpTools.TransmitOrder("helm warp 9", false, cancellationToken);
        var json = await BridgeMcpTools.TransmitOrder("clear queue", true, cancellationToken)
            .ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TransmitResult>(json);
        Assert(
            result?.StatusLine.Contains("cleared", StringComparison.OrdinalIgnoreCase) == true,
            "clear queue status");
        var stopJson = await BridgeMcpTools.TransmitOrder("helm full stop", true, cancellationToken)
            .ConfigureAwait(false);
        var stop = JsonSerializer.Deserialize<TransmitResult>(stopJson);
        Assert(stop?.Snapshot.SpeedWarp == 0, "full stop after clear");
    }

    private static async Task TestBelayViaMcp(CancellationToken cancellationToken)
    {
        BridgeMcpRuntime.ResetSession();
        _ = BridgeMcpTools.TransmitOrder("helm set heading 045", false, cancellationToken);
        await Task.Delay(150, cancellationToken).ConfigureAwait(false);
        var json = await BridgeMcpTools.TransmitOrder("belay that", true, cancellationToken)
            .ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TransmitResult>(json);
        Assert(
            result?.StatusLine.Contains("Belay", StringComparison.OrdinalIgnoreCase) == true,
            "belay acknowledged");
    }

    private static BridgeSnapshot DeserializeSnapshot(string json) =>
        JsonSerializer.Deserialize<BridgeSnapshot>(json)
        ?? throw new InvalidOperationException("Invalid snapshot JSON.");

    private static void Assert(bool condition, string message)
    {
        if (condition)
        {
            _passed++;
            return;
        }

        _failed++;
        Failures.Add(message);
    }
}
