using Novolis.Commands;
using Novolis.Commands.Engine;
using Novolis.Commands.Queueing;

namespace BridgeCommander.Bridge;

public sealed class BridgeCommandService : IAsyncDisposable
{
    private readonly ICommandEngine<BridgeState> _engine;
    private readonly ICommandQueue _queue;
    private readonly CommandQueueRunner<BridgeState> _runner;
    private readonly BridgeActivityTracker _activity;
    private readonly CancellationTokenSource _runCts = new();

    public BridgeCommandService(BridgeState state, BridgeActivityTracker activity, BridgeVoiceAnnouncer announcer)
    {
        _activity = activity;

        var engineOptions = new CommandEngineOptions();
        engineOptions.ArgumentParsers.Register("heading3d", new BridgeHeadingArgumentParser());

        _engine = new CommandEngine<BridgeState>(
            BridgeCommandRegistry.Create(),
            new BridgeContextResolver(),
            engineOptions);

        _queue = new ChannelCommandQueue();
        _runner = new CommandQueueRunner<BridgeState>(
            _queue,
            new BridgeCommandProcessor(activity, announcer));
        _ = Task.Run(() => _runner.RunAsync(state, _runCts.Token));
    }

    public async Task SubmitPromptAsync(BridgeState state, string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        var trimmed = prompt.Trim();
        var result = await _engine.ParseCommandAsync(trimmed, state);

        if (result.Success && result.Command is not null)
        {
            if (result.Command.Name == BuiltInCommands.Help)
            {
                ApplyHelp(state, trimmed, result.Command.Arguments["topic"] as string);
                return;
            }

            state.AddHistory(new HistoryEntry(
                DateTimeOffset.UtcNow,
                trimmed,
                HistoryKind.ParseSuccess,
                $"Queued {result.Command.Name}"));

            _activity.OnEnqueued();
            await _queue.EnqueueAsync(result.Command);
            state.StatusLine = $"Queued {result.Command.Name}.";
            return;
        }

        if (result.Failures.Any(f => f.Code == ParseFailureCode.AmbiguousCommand))
        {
            var details = result.Candidates
                .Select(c => $"{c.Name} ({c.Confidence:P0}) — {c.Reason}")
                .ToList();

            state.AddHistory(new HistoryEntry(
                DateTimeOffset.UtcNow,
                trimmed,
                HistoryKind.ParseFailure,
                "Ambiguous command.",
                details));

            state.StatusLine = "Ambiguous — see log for candidates.";
            return;
        }

        var failure = result.Failures.FirstOrDefault();
        var failureDetails = failure?.Code == ParseFailureCode.UnknownContext
            ? ["Prefixes: helm, tactical, weaps, pilot, eng, nav, comms, admin"]
            : result.Suggestions.Count > 0
                ? result.Suggestions.Select(s => $"Did you mean: {s}?").ToList()
                : [];

        state.AddHistory(new HistoryEntry(
            DateTimeOffset.UtcNow,
            trimmed,
            HistoryKind.ParseFailure,
            failure?.Message ?? "Parse failed.",
            failureDetails));

        state.StatusLine = failure?.Message ?? "Unable to parse command.";
    }

    private static void ApplyHelp(BridgeState state, string prompt, string? topic)
    {
        state.HelpPanelLines.Clear();
        state.HelpPanelLines.AddRange(BridgeHelp.GetLines(topic));
        state.AddHistory(new HistoryEntry(
            DateTimeOffset.UtcNow,
            prompt,
            HistoryKind.Help,
            string.IsNullOrEmpty(topic) ? "Opened command reference." : $"Help: {topic}."));
        state.StatusLine = "Help updated in reference panel.";
    }

    public ValueTask DisposeAsync()
    {
        _runCts.Cancel();
        _runCts.Dispose();
        return ValueTask.CompletedTask;
    }
}
