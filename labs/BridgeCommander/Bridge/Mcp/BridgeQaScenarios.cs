namespace BridgeCommander.Bridge.Mcp;

public static class BridgeQaScenarios
{
    public static IReadOnlyList<string> Names { get; } =
    [
        "natural-orders",
        "kr12-engagement",
        "belay-interrupt",
        "clear-queue",
        "parse-failures"
    ];

    public static async Task<BridgeQaReport> RunAsync(
        BridgeSession session,
        string scenario,
        CancellationToken cancellationToken = default)
    {
        if (!Names.Contains(scenario, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Unknown scenario '{scenario}'. Available: {string.Join(", ", Names)}.");
        }

        session.Initialize();
        var steps = new List<BridgeQaStepResult>();

        switch (scenario.ToLowerInvariant())
        {
            case "natural-orders":
                steps.Add(await Step(session, "helm come about", s => s.Heading is >= 359 or <= 1,
                    cancellationToken));
                steps.Add(await Step(session, "helm all ahead full", s => s.SpeedWarp == 9, cancellationToken));
                steps.Add(await Step(session, "weaps target the closest enemy", s => s.TargetLocked, cancellationToken));
                steps.Add(await Step(session, "helm, set heading to 122 by 180",
                    s => Math.Abs(s.Heading - 122) < 0.01 && Math.Abs(s.HeadingBy - 180) < 0.01, cancellationToken));
                steps.Add(await Step(session, "helm course 122 by 33",
                    s => Math.Abs(s.Heading - 122) < 0.01 && Math.Abs(s.HeadingBy - 33) < 0.01, cancellationToken));
                break;

            case "kr12-engagement":
                steps.Add(await Step(session, "tactical target the closest enemy", s => s.TargetName?.Contains("KR-12") == true,
                    cancellationToken));
                steps.Add(await Step(session, "tactical fire weapons", s => s.StatusLine.Contains("fired", StringComparison.OrdinalIgnoreCase),
                    cancellationToken));
                steps.Add(await Step(session, "eng divert shields", s => s.ShieldPercent >= 80, cancellationToken));
                break;

            case "belay-interrupt":
                _ = session.TransmitAsync("helm set heading 010", waitForIdle: false, cancellationToken: cancellationToken);
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                steps.Add(await Step(session, "belay that",
                    s => s.StatusLine.Contains("Belay", StringComparison.OrdinalIgnoreCase), cancellationToken));
                break;

            case "clear-queue":
                _ = session.TransmitAsync("helm warp 5", waitForIdle: false, cancellationToken: cancellationToken);
                _ = session.TransmitAsync("helm warp 7", waitForIdle: false, cancellationToken: cancellationToken);
                steps.Add(await Step(session, "clear queue",
                    s => s.StatusLine.Contains("cleared", StringComparison.OrdinalIgnoreCase), cancellationToken));
                steps.Add(await Step(session, "helm full stop", s => s.SpeedWarp == 0, cancellationToken));
                break;

            case "parse-failures":
                steps.Add(await ExpectFailure(session, "heading 270", cancellationToken));
                steps.Add(await ExpectFailure(session, "helm explode", cancellationToken));
                steps.Add(await Step(session, "help tactical", s => s.HelpLines.Count > 3, cancellationToken));
                break;
        }

        return new BridgeQaReport(scenario, steps.All(s => s.Passed), steps);
    }

    private static async Task<BridgeQaStepResult> Step(
        BridgeSession session,
        string prompt,
        Func<BridgeSnapshot, bool> assert,
        CancellationToken cancellationToken)
    {
        var result = await session.TransmitAsync(prompt, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var passed = result.ParseSucceeded && assert(result.Snapshot);
        return new BridgeQaStepResult(prompt, passed, result.StatusLine, result.Snapshot);
    }

    private static async Task<BridgeQaStepResult> ExpectFailure(
        BridgeSession session,
        string prompt,
        CancellationToken cancellationToken)
    {
        var result = await session.TransmitAsync(prompt, waitForIdle: false, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var passed = !result.ParseSucceeded;
        return new BridgeQaStepResult(prompt, passed, result.StatusLine, result.Snapshot);
    }
}

public sealed record BridgeQaStepResult(
    string Prompt,
    bool Passed,
    string StatusLine,
    BridgeSnapshot Snapshot);

public sealed record BridgeQaReport(
    string Scenario,
    bool Passed,
    IReadOnlyList<BridgeQaStepResult> Steps);
