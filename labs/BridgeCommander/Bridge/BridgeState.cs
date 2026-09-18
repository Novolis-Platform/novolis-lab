using System.Globalization;
using Novolis.Commands;

namespace BridgeCommander.Bridge;
public sealed class BridgeState
{
    public const int MaxHistory = 200;

    public string ShipName { get; } = "USS Novolis";
    public double Heading { get; set; } = 180;
    public double HeadingBy { get; set; }
    public int SpeedWarp { get; set; } = 5;
    public int ShieldPercent { get; set; } = 80;
    public int HullPercent { get; set; } = 100;
    public bool TargetLocked { get; set; }
    public string? TargetName { get; set; }
    public string StatusLine { get; set; } = "Standing by. Prefix orders with a station (helm, tactical, weaps…).";
    public string? LastExecutedCommand { get; set; }
    public CommandEnvelope? LastEnvelope { get; set; }

    public List<HistoryEntry> History { get; } = [];
    public List<string> HelpPanelLines { get; } = ["Type help for command reference."];

    public string PromptDraft { get; set; } = "";

    public void AddHistory(HistoryEntry entry)
    {
        History.Add(entry);
        if (History.Count > MaxHistory)
            History.RemoveAt(0);
    }

    public IReadOnlyList<string> FormatHistory()
    {
        var lines = new List<string>();
        foreach (var entry in History)
        {
            lines.Add(entry.DisplayLine);
            if (entry.Kind != HistoryKind.ParseFailure || entry.Details is not { Count: > 0 })
                continue;

            foreach (var detail in entry.Details)
                lines.Add(string.IsNullOrEmpty(detail) ? "" : $"  {detail}");
        }

        return lines;
    }

    public string FormatStatus()
    {
        var hdg = FormatAxis(Heading);
        var by = FormatAxis(HeadingBy);
        return $"HDG {hdg} BY {by}  |  Warp {SpeedWarp}  |  Shields {ShieldPercent}%  |  Hull {HullPercent}%  |  " +
               $"Target: {(TargetLocked ? TargetName ?? "locked" : "none")}";
    }

    private static string FormatAxis(double degrees)
    {
        var normalized = degrees % 360;
        if (normalized < 0)
            normalized += 360;

        return Math.Abs(normalized - Math.Round(normalized)) < 0.001
            ? $"{normalized.ToString("0", CultureInfo.InvariantCulture)}°"
            : $"{normalized.ToString("0.0", CultureInfo.InvariantCulture)}°";
    }
}

