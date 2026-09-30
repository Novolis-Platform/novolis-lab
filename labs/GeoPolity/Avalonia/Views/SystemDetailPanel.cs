using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using GeoPolity.Presentation;
using GeoPolity.Session;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Agent.Protocol;
using Novolis.Avalonia.Briefing;
using Novolis.Avalonia.Controls;
using Novolis.Geopolitics.Core;

namespace GeoPolity.AvaloniaUi.Views;

/// <summary>Right rail: selected system, habitats, force, player build controls.</summary>
internal sealed class SystemDetailPanel : UserControl
{
    private static readonly IBrush PanelBg = new SolidColorBrush(Color.Parse("#122636"));
    private static readonly IBrush Copper = new SolidColorBrush(Color.Parse("#c47b3a"));
    private static readonly IBrush Fog = new SolidColorBrush(Color.Parse("#c8d4dc"));
    private static readonly IBrush Teal = new SolidColorBrush(Color.Parse("#2a9d8f"));

    private readonly TextBlock _title = new() { FontSize = 16, FontWeight = FontWeight.Bold, Foreground = Teal };
    private readonly TextBlock _meta = new() { FontSize = 12, Foreground = Fog, TextWrapping = TextWrapping.Wrap };
    private readonly MetricTableView _stats = new();
    private readonly ListBox _habitats = new() { MinHeight = 120, MaxHeight = 180 };
    private readonly MetricTableView _force = new();
    private readonly Slider _milShare = new()
    {
        Minimum = 0.05,
        Maximum = 0.7,
        TickFrequency = 0.05,
        IsSnapToTickEnabled = true,
        Width = 200,
    };
    private readonly TextBlock _milShareLabel = new() { Foreground = Fog, FontSize = 12 };
    private readonly TextBlock _buildNote = new() { Foreground = Copper, FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private GeoSession? _session;
    private bool _suppressSlider;

    public event Action? Changed;

    public SystemDetailPanel()
    {
        Background = PanelBg;
        Padding = new Thickness(10);
        var buildRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                MakeBuildButton("Land +10", "geopolity.build.land", Novolis.Geopolitics.Core.MilitaryDomain.Land),
                MakeBuildButton("Air +10", "geopolity.build.air", Novolis.Geopolitics.Core.MilitaryDomain.Air),
                MakeBuildButton("Naval +10", "geopolity.build.naval", Novolis.Geopolitics.Core.MilitaryDomain.Naval),
            },
        };

        _milShare.PropertyChanged += (_, e) =>
        {
            if (_suppressSlider || e.Property != RangeBase.ValueProperty || _session is null)
            {
                return;
            }

            GeoSessionCommands.SetMilitaryShare(_session, _milShare.Value);
            Changed?.Invoke();
        };
        AgentProperties.SetId(_milShare, "geopolity.milshare");
        AgentProperties.SetId(_habitats, "geopolity.habitats");
        AgentProperties.SetRole(_habitats, AgentRoleNames.ListBox);

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    Header("SYSTEM"),
                    _title,
                    _meta,
                    _stats,
                    Header("HABITATS"),
                    _habitats,
                    Header("FORCE"),
                    _force,
                    Header("PLAYER BUILD"),
                    _milShareLabel,
                    _milShare,
                    buildRow,
                    _buildNote,
                },
            },
        };
    }

    public void Bind(GeoSession session)
    {
        _session = session;
        var sel = session.Selected;
        var isPlayer = sel.Id == session.PlayerSystemId;
        _title.Text = sel.Name + (isPlayer ? "  ★" : "");
        _meta.Text =
            $"Cluster {sel.Continent} · {sel.Government} · tech {sel.TechLevel:0.0}\n" +
            $"GDP {sel.Gdp:0} · treasury {sel.Treasury:0} · power {sel.PowerScore:0}";

        _stats.SetRows(
        [
            new MetricRow("Legitimacy", sel.Civic.Legitimacy.ToString("0.00"), null),
            new MetricRow("Approval", sel.Civic.Approval.ToString("0.00"), null),
            new MetricRow("HD", sel.Civic.HumanDevelopment.ToString("0.00"), null),
            new MetricRow("Corruption", sel.Civic.Corruption.ToString("0.00"), null),
            new MetricRow("Stability", sel.Stability.ToString("0.00"), null),
            new MetricRow("Tax", sel.Policy.HouseholdTaxRate.ToString("0%"), null),
        ]);

        var habitats = session.World.Provinces
            .Where(p => p.HomePolityId == sel.Id || p.OwnerId == sel.Id)
            .OrderBy(p => p.Id.Value)
            .Take(12)
            .Select(p =>
            {
                var owned = p.OwnerId == sel.Id ? "" : "!";
                return new MarkedListRow(
                    owned,
                    TheatreLabels.HabitatTag(p),
                    p.Name,
                    $"pop {p.Population / 1_000_000:0.0}M",
                    p.Id);
            });
        _habitats.Items.Clear();
        foreach (var row in habitats)
        {
            _habitats.Items.Add(new ListBoxItem
            {
                Content = MarkedListBox.CreateItem(row),
                Tag = row,
            });
        }

        _force.SetRows(
        [
            new MetricRow("Land", sel.Military.Land.ToString("0"), null),
            new MetricRow("Air", sel.Military.Air.ToString("0"), null),
            new MetricRow("Naval", sel.Military.Naval.ToString("0"), null),
            new MetricRow("Total", sel.Military.Total.ToString("0"), null),
        ]);

        _suppressSlider = true;
        _milShare.Value = session.Player.Policy.MilitaryShare;
        _milShare.IsEnabled = true;
        _suppressSlider = false;
        _milShareLabel.Text = $"Mil budget share {session.Player.Policy.MilitaryShare:0%} (player system)";
        _buildNote.Text = isPlayer
            ? "Build spends your treasury immediately."
            : $"Viewing {sel.Name}. Builds apply to your system ({session.Player.Name}).";
    }

    private Button MakeBuildButton(string label, string agentId, Novolis.Geopolitics.Core.MilitaryDomain domain)
    {
        var btn = new Button
        {
            Content = label,
            Padding = new Thickness(8, 4),
            Background = new SolidColorBrush(Color.Parse("#163447")),
            Foreground = Fog,
            BorderBrush = Teal,
            BorderThickness = new Thickness(1),
        };
        btn.Click += (_, _) =>
        {
            if (_session is null)
            {
                return;
            }

            GeoSessionCommands.OrderBuild(_session, domain, 10);
            Changed?.Invoke();
        };
        AgentProperties.SetId(btn, agentId);
        AgentProperties.SetRole(btn, AgentRoleNames.Button);
        return btn;
    }

    private static TextBlock Header(string text) => new()
    {
        Text = text,
        Foreground = Copper,
        FontWeight = FontWeight.Bold,
        FontSize = 12,
        LetterSpacing = 1.1,
    };
}
