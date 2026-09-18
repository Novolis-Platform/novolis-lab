using Novolis.Audio.Voice;

namespace BridgeCommander.Bridge;

/// <summary>
/// Headless bridge session shared by Spectre UI and MCP automation.
/// </summary>
public sealed class BridgeSession : IAsyncDisposable
{
    public BridgeState State { get; }
    public BridgeCommandService Commands { get; }
    public BridgeActivityTracker Activity { get; }
    public bool VoiceEnabled { get; }
    public BridgeVoiceCast? VoiceCast { get; private set; }
    public IVoiceService? Voice { get; }
    public BridgeVoiceAnnouncer Announcer { get; }

    private BridgeSession(
        BridgeState state,
        BridgeCommandService commands,
        BridgeActivityTracker activity,
        bool voiceEnabled,
        BridgeVoiceCast? voiceCast,
        IVoiceService? voice,
        BridgeVoiceAnnouncer announcer)
    {
        State = state;
        Commands = commands;
        Activity = activity;
        VoiceEnabled = voiceEnabled;
        VoiceCast = voiceCast;
        Voice = voice;
        Announcer = announcer;
    }

    public static BridgeSession Create(BridgeSessionOptions? options = null)
    {
        options ??= BridgeSessionOptions.Default;
        var state = new BridgeState();
        var activity = new BridgeActivityTracker();

        BridgeVoiceCast? cast = null;
        IVoiceService? voice = null;
        if (options.VoiceEnabled)
        {
            if (options.UseCharacterVoices)
            {
                cast = BridgeVoiceCast.Create(true);
                voice = null;
            }
            else
            {
                voice = BridgeVoice.CreateService(
                    true,
                    options.VoiceArchetype,
                    options.AtcDelivery,
                    options.ApplyAtcDelivery);
            }
        }

        var announcer = new BridgeVoiceAnnouncer(voice, options.AwaitVoicePlayback);
        var commands = new BridgeCommandService(state, activity, announcer);
        var session = new BridgeSession(state, commands, activity, options.VoiceEnabled, cast, voice, announcer);
        session.Initialize();
        return session;
    }

    public void Initialize()
    {
        State.History.Clear();
        State.HelpPanelLines.Clear();
        State.HelpPanelLines.AddRange(BridgeHelp.GetLines(null));
        State.AddHistory(new HistoryEntry(
            DateTimeOffset.UtcNow,
            "",
            HistoryKind.System,
            "Bridge online. All stations standing by."));
        State.StatusLine = "Standing by for orders. Prefix with helm, tactical, weaps, engineering…";
        State.Heading = 180;
        State.HeadingBy = 0;
        State.SpeedWarp = 5;
        State.ShieldPercent = 80;
        State.HullPercent = 100;
        State.TargetLocked = false;
        State.TargetName = null;
        State.LastExecutedCommand = null;
        State.LastEnvelope = null;
        Activity.Reset();
    }

    public BridgeSnapshot GetSnapshot() => BridgeSnapshot.From(State, Activity);

    public async Task<TransmitResult> TransmitAsync(
        string prompt,
        bool waitForIdle = true,
        TimeSpan? idleTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var historyBefore = State.History.Count;
        await Commands.SubmitPromptAsync(State, prompt).ConfigureAwait(false);

        if (waitForIdle)
        {
            await Activity.WaitForIdleAsync(
                idleTimeout ?? TimeSpan.FromSeconds(30),
                cancellationToken).ConfigureAwait(false);
        }

        var newEntries = State.History.Skip(historyBefore).ToArray();
        return TransmitResult.From(prompt, newEntries, GetSnapshot());
    }

    public async ValueTask DisposeAsync()
    {
        await Commands.DisposeAsync().ConfigureAwait(false);
        if (VoiceCast is not null)
            await VoiceCast.DisposeAsync().ConfigureAwait(false);
    }
}

public sealed record TransmitResult(
    string Prompt,
    bool ParseSucceeded,
    bool Executed,
    string StatusLine,
    IReadOnlyList<string> NewHistoryLines,
    BridgeSnapshot Snapshot)
{
    public static TransmitResult From(
        string prompt,
        IReadOnlyList<HistoryEntry> newEntries,
        BridgeSnapshot snapshot)
    {
        var parseOk = newEntries.Any(e => e.Kind is HistoryKind.ParseSuccess or HistoryKind.Help);
        var executed = newEntries.Any(e => e.Kind is HistoryKind.Executed or HistoryKind.Interrupted);
        var failed = newEntries.Any(e => e.Kind is HistoryKind.ParseFailure);

        return new TransmitResult(
            prompt,
            parseOk && !failed,
            executed,
            snapshot.StatusLine,
            newEntries.SelectMany(FormatHistoryEntry).ToArray(),
            snapshot);
    }

    private static IEnumerable<string> FormatHistoryEntry(HistoryEntry entry)
    {
        yield return entry.DisplayLine;
        if (entry.Details is null)
            yield break;

        foreach (var detail in entry.Details)
            yield return string.IsNullOrEmpty(detail) ? "" : $"  {detail}";
    }
}
