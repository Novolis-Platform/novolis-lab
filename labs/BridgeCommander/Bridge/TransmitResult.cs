using Novolis.Audio.Voice;

namespace BridgeCommander.Bridge;

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
