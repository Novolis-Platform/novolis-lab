using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GeoPolity.Session;
using Novolis.Avalonia.Briefing;
using Novolis.Geopolitics.Core;

namespace GeoPolity.AvaloniaUi.Views;

/// <summary>Left rail: clock, campaign scorecard, cluster/power metrics.</summary>
internal sealed class CommandPanel : UserControl
{
    private static readonly IBrush Navy = new SolidColorBrush(Color.Parse("#0b1c2c"));
    private static readonly IBrush Teal = new SolidColorBrush(Color.Parse("#2a9d8f"));
    private static readonly IBrush Copper = new SolidColorBrush(Color.Parse("#c47b3a"));
    private static readonly IBrush Fog = new SolidColorBrush(Color.Parse("#c8d4dc"));

    private readonly TextBlock _date = MakeValue();
    private readonly TextBlock _clock = MakeValue();
    private readonly TextBlock _player = MakeValue();
    private readonly TextBlock _status = MakeMuted();
    private readonly DualMetricStrip _civicStrip = new()
    {
        LeftLabel = "Legitimacy",
        RightLabel = "Approval",
        Caption = "World mean civics",
        Margin = new Thickness(0, 6, 0, 6),
    };
    private readonly ScorecardView _scorecard = new();
    private readonly MetricTableView _clusters = new();
    private readonly MetricTableView _power = new();

    public CommandPanel()
    {
        Background = Navy;
        Padding = new Thickness(10);
        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    Header("BRIDGE"),
                    Row("Date", _date),
                    Row("Clock", _clock),
                    Row("Player", _player),
                    _status,
                    _civicStrip,
                    Header("CAMPAIGN"),
                    _scorecard,
                    Header("CLUSTERS"),
                    _clusters,
                    Header("POWER"),
                    _power,
                },
            },
        };
    }

    public void Bind(GeoSession session)
    {
        var world = session.World;
        var sim = session.Simulation;
        var clock = session.Clock;
        var player = session.Player;

        _date.Text = $"Y{world.Year} · M{world.Month + 1} · D{world.DayOfYear + 1}";
        _clock.Text = clock.Running
            ? $"RUN  {clock.DaysPerPulse}d ({clock.SpeedLabel})"
            : "PAUSE";
        _clock.Foreground = clock.Running ? Teal : Copper;
        _player.Text = $"{player.Name}  treasury {player.Treasury:0}";
        _status.Text = clock.StatusNote ?? "";
        _civicStrip.LeftValue = sim.Telemetry.MeanLegitimacy.ToString("0.00");
        _civicStrip.RightValue = sim.Telemetry.MeanApproval.ToString("0.00");

        var wars = world.ActiveWars.Count();
        var cm = world.CountActiveTreatiesOfKind(TreatyKind.CommonMarket);
        var alliances = world.CountActiveTreatiesOfKind(TreatyKind.Alliance);
        _scorecard.SetRows(
            [
                new ScorecardRow("wars", wars, $"{wars} active wars", wars > 0),
                new ScorecardRow("markets", cm, $"{cm} common markets", cm > 0),
                new ScorecardRow("allies", alliances, $"{alliances} alliances", alliances > 0),
                new ScorecardRow("captures", sim.Telemetry.ProvincesCaptured, $"{sim.Telemetry.ProvincesCaptured} habitats taken", sim.Telemetry.ProvincesCaptured > 0),
            ],
            "Theatre");

        _clusters.SetRows(
            world.Polities
                .GroupBy(p => p.Continent)
                .OrderByDescending(g => g.Sum(p => p.PowerScore))
                .Take(8)
                .Select(g =>
                {
                    var w = world.ActiveWars.Count(x =>
                        world.Polity(x.Attacker).Continent == g.Key
                        || world.Polity(x.Defender).Continent == g.Key);
                    return new MetricRow(g.Key, $"{g.Count()} sys · {w}w", $"power {g.Sum(p => p.PowerScore):0}");
                }));

        _power.SetRows(
            world.Polities.OrderByDescending(p => p.PowerScore).Take(5)
                .Select((p, i) => new MetricRow(
                    $"{i + 1}. {Truncate(p.Name, 12)}",
                    $"{p.PowerScore:0}",
                    $"L{p.Civic.Legitimacy:0.00}")));
    }

    private static Control Row(string label, TextBlock value) =>
        new DockPanel
        {
            LastChildFill = true,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Width = 64,
                    Foreground = Teal,
                    FontSize = 12,
                    [DockPanel.DockProperty] = Dock.Left,
                },
                value,
            },
        };

    private static TextBlock Header(string text) => new()
    {
        Text = text,
        Foreground = Copper,
        FontWeight = FontWeight.Bold,
        FontSize = 12,
        LetterSpacing = 1.2,
        Margin = new Thickness(0, 6, 0, 2),
    };

    private static TextBlock MakeValue() => new()
    {
        Foreground = Fog,
        FontSize = 13,
        TextWrapping = TextWrapping.Wrap,
    };

    private static TextBlock MakeMuted() => new()
    {
        Foreground = new SolidColorBrush(Color.Parse("#7a8a96")),
        FontSize = 11,
    };

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}
