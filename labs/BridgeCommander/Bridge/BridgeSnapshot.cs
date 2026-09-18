namespace BridgeCommander.Bridge;

public sealed record BridgeSnapshot(
    string ShipName,
    double Heading,
    double HeadingBy,
    int SpeedWarp,
    int ShieldPercent,
    int HullPercent,
    bool TargetLocked,
    string? TargetName,
    string StatusLine,
    string? LastExecutedCommand,
    string StatusFormatted,
    IReadOnlyList<string> HistoryLines,
    IReadOnlyList<string> HelpLines,
    bool IsIdle)
{
    public static BridgeSnapshot From(BridgeState state, BridgeActivityTracker activity) =>
        new(
            state.ShipName,
            state.Heading,
            state.HeadingBy,
            state.SpeedWarp,
            state.ShieldPercent,
            state.HullPercent,
            state.TargetLocked,
            state.TargetName,
            state.StatusLine,
            state.LastExecutedCommand,
            state.FormatStatus(),
            state.FormatHistory().ToArray(),
            state.HelpPanelLines.ToArray(),
            activity.IsIdle);
}
