namespace BridgeCommander.Bridge;

public enum HistoryKind
{
    System,
    Help,
    ParseSuccess,
    ParseFailure,
    Executing,
    Executed,
    Interrupted
}

public sealed record HistoryEntry(
    DateTimeOffset At,
    string Prompt,
    HistoryKind Kind,
    string Summary,
    IReadOnlyList<string>? Details = null)
{
    public string DisplayLine =>
        $"[{At:HH:mm:ss}] {KindTag} {PromptDisplay} — {Summary}";

    private string KindTag => Kind switch
    {
        HistoryKind.ParseSuccess => "PARSE",
        HistoryKind.ParseFailure => "FAIL",
        HistoryKind.Executing => "EXEC",
        HistoryKind.Executed => "DONE",
        HistoryKind.Interrupted => "STOP",
        HistoryKind.System => "SYS",
        HistoryKind.Help => "HELP",
        _ => "?"
    };

    private string PromptDisplay =>
        string.IsNullOrWhiteSpace(Prompt) ? "(system)" : Prompt;
}
