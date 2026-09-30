using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Astro.Abstractions;
using Novolis.Astro.Assessment;
using Novolis.Astro.Catalog;
using Novolis.Astro.Overlay;
using Novolis.Astro.Routing;
using Novolis.Avalonia.StarMap;
using Novolis.Avalonia.Studio;
using Novolis.Physics.Astro;

namespace StarMapLab;

internal sealed class MainWindow : Window
{
    // Prototype-compatible bands: ≤10 ly @ 1× cost, ≤12 ly @ 3× cost.
    // Long single hops are intentionally more expensive than several short ones
    // (see FTL operational tradeoffs: distance cost is not linear in practice).
    const double MaxRangeLy = 12;
    const double ShortBandMaxLy = 10;
    const double ShortCostPerLy = 1.0;
    const double LongCostPerLy = 3.0;

    readonly StarMapControl _map;
    readonly TextBlock _routeSummary;
    readonly TextBlock _systemDetail;
    readonly TextBlock _overlayDetail;
    readonly TextBlock _jumpDetail;
    readonly ListBox _jumpList;
    readonly ComboBox _fromBox;
    readonly ComboBox _toBox;
    readonly CheckBox _showGraphBox;
    readonly StarCatalog _catalog;
    readonly CatalogOverlay _overlay;
    readonly RouteGraph _graph;
    readonly HabitabilityAssessor _habit = new();
    readonly StrategicValueAssessor _strategic = new();
    readonly StudioFeedback _feedback;
    readonly List<JumpInfo> _jumps = [];
    HashSet<string> _routeEdgeKeys = new(StringComparer.OrdinalIgnoreCase);
    string? _fromId;
    string? _toId;
    string? _inspectId;
    bool _suppressJumpSelection;

    public MainWindow()
    {
        Title = "Novolis Star Map Lab";
        Width = 1360;
        Height = 860;

        _catalog = DemoCatalog.Create();
        _overlay = DemoOverlay.Create();
        var cost = RangeBandCostModel.CreatePrototypeCompatible();
        _graph = RouteGraph.Build(_catalog.All, MaxRangeLy, cost);

        var chrome = StudioChrome.Create();
        _feedback = chrome.CreateFeedback();

        _map = new StarMapControl { Margin = new Thickness(8) };
        _map.StarSelected += OnStarSelected;

        _routeSummary = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            FontFamily = new FontFamily("Consolas, Courier New, monospace")
        };
        _systemDetail = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.9
        };
        _overlayDetail = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.85,
            Margin = new Thickness(0, 8, 0, 0)
        };
        _jumpDetail = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            FontFamily = new FontFamily("Consolas, Courier New, monospace"),
            Margin = new Thickness(0, 8, 0, 0)
        };
        _jumpList = new ListBox
        {
            Height = 220,
            Margin = new Thickness(0, 4, 0, 0)
        };
        _jumpList.SelectionChanged += OnJumpSelectionChanged;

        var ids = _catalog.All
            .OrderBy(s => s.Coords.DistanceFromOrigin)
            .Select(s => s.Id.Value)
            .ToList();
        _fromBox = MakeSystemCombo(ids);
        _toBox = MakeSystemCombo(ids);
        _fromId = "sol";
        _toId = "altair";
        _inspectId = _fromId;
        _fromBox.SelectedItem = _fromId;
        _toBox.SelectedItem = _toId;
        _fromBox.SelectionChanged += (_, _) =>
        {
            _fromId = _fromBox.SelectedItem as string;
            Replan();
        };
        _toBox.SelectionChanged += (_, _) =>
        {
            _toId = _toBox.SelectedItem as string;
            Replan();
        };

        _showGraphBox = new CheckBox
        {
            Content = "List all graph jumps",
            IsChecked = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        _showGraphBox.IsCheckedChanged += (_, _) => Replan();

        var planBtn = new Button { Content = "Plan route", Margin = new Thickness(8, 0, 0, 0) };
        planBtn.Click += (_, _) => Replan();

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(12, 12, 12, 0),
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = $"{_catalog.Count} systems · bands short≤{ShortBandMaxLy:0}@{ShortCostPerLy:0}× / long≤{MaxRangeLy:0}@{LongCostPerLy:0}×",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 12, 0)
                },
                new TextBlock { Text = "From", VerticalAlignment = VerticalAlignment.Center },
                _fromBox,
                new TextBlock { Text = "To", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) },
                _toBox,
                planBtn,
                _showGraphBox
            }
        };

        var sideScroll = new ScrollViewer
        {
            Width = 380,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new StackPanel
            {
                Margin = new Thickness(0, 8, 12, 8),
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = "System", FontWeight = FontWeight.Bold },
                    _systemDetail,
                    new TextBlock { Text = "Overlay", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 12, 0, 0) },
                    _overlayDetail,
                    new TextBlock { Text = "Route (parsable)", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 12, 0, 0) },
                    _routeSummary,
                    new TextBlock { Text = "Jumps (select one)", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 12, 0, 0) },
                    _jumpList,
                    new TextBlock { Text = "Jump detail (parsable)", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 12, 0, 0) },
                    _jumpDetail
                }
            }
        };

        var mapRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(_map, 0);
        Grid.SetColumn(sideScroll, 1);
        mapRow.Children.Add(_map);
        mapRow.Children.Add(sideScroll);

        var statusBar = new DockPanel();
        DockPanel.SetDock(chrome.FlashLine, Dock.Bottom);
        DockPanel.SetDock(chrome.StatusLine, Dock.Bottom);
        statusBar.Children.Add(chrome.FlashLine);
        statusBar.Children.Add(chrome.StatusLine);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Grid.SetRow(toolbar, 0);
        Grid.SetRow(mapRow, 1);
        Grid.SetRow(statusBar, 2);
        root.Children.Add(toolbar);
        root.Children.Add(mapRow);
        root.Children.Add(statusBar);

        Content = root;
        Opened += (_, _) =>
        {
            RefreshOverlayPanel();
            RefreshSystemPanel(_inspectId);
            Replan();
            _feedback.Flash("Select a jump in the list for parsable hop detail. Long hops cost 3× (≤12 ly); short ≤10 ly at 1×.");
        };
    }

    void OnStarSelected(string id)
    {
        _inspectId = id;
        RefreshSystemPanel(id);

        if (_fromId is null || string.Equals(_fromId, id, StringComparison.OrdinalIgnoreCase))
        {
            _fromId = id;
            _fromBox.SelectedItem = id;
            return;
        }

        _toId = id;
        _toBox.SelectedItem = id;
        Replan();
    }

    void OnJumpSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressJumpSelection)
            return;
        if (_jumpList.SelectedItem is not JumpInfo jump)
        {
            _jumpDetail.Text = "Select a jump.";
            return;
        }

        _jumpDetail.Text = jump.ToParsable();
        _inspectId = jump.FromId;
        _map.SelectedId = jump.FromId;
        RefreshSystemPanel(jump.FromId);
        _feedback.SetStatus($"jump {jump.Index}: {jump.FromId} → {jump.ToId} band={jump.Band} cost={Fmt(jump.Cost)}");
    }

    void RefreshOverlayPanel()
    {
        var errors = _overlay.Validate(_catalog);
        var lines = new List<string>
        {
            errors.Count == 0
                ? $"{_overlay.Entries.Count} aliases — valid"
                : $"validation: {string.Join("; ", errors)}"
        };
        foreach (var entry in _overlay.Entries.OrderBy(e => e.Alias, StringComparer.OrdinalIgnoreCase))
        {
            var role = entry.Labels is not null && entry.Labels.TryGetValue("role", out var r) ? r : "-";
            lines.Add($"{entry.Alias} → {entry.CatalogSystemId.Value} ({role})");
        }

        _overlayDetail.Text = string.Join('\n', lines);
    }

    void RefreshSystemPanel(string? id)
    {
        if (id is null || !_catalog.TryGet(id, out var system) || system is null)
        {
            _systemDetail.Text = "Select a star.";
            return;
        }

        var h = _habit.Assess(system);
        var s = _strategic.Assess(system);
        var tags = system.Tags.Count == 0 ? "(none)" : string.Join(", ", system.Tags);
        var meters = AstronomicalUnits.LyToMeters(system.Coords.DistanceFromOrigin);
        var neighbors = _catalog.NeighborsWithin(system.Coords, MaxRangeLy, system.Id);
        var neighborText = neighbors.Count == 0
            ? "none within hop range"
            : string.Join(", ", neighbors.Take(6).Select(n => $"{n.System.Id.Value} ({n.DistanceLy:0.#} ly)"));

        var aliases = _overlay.Entries
            .Where(e => string.Equals(e.CatalogSystemId.Value, system.Id.Value, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Alias)
            .ToList();
        var aliasText = aliases.Count == 0 ? "—" : string.Join(", ", aliases);

        _systemDetail.Text =
            $"{system.Name} ({system.Id.Value})\n" +
            $"Spectral {system.SpectralClass} · {system.Coords.DistanceFromOrigin:0.##} ly from Sol ({meters:E2} m)\n" +
            $"Habitability {h.Score:0}/{h.Tier} — {string.Join("; ", h.Reasons)}\n" +
            $"Strategic {s.Score:0}/{s.Tier} — {string.Join("; ", s.Reasons)}\n" +
            $"Tags: {tags}\n" +
            $"Aliases: {aliasText}\n" +
            $"Neighbors ≤{MaxRangeLy:0} ly: {neighborText}";
    }

    void Replan()
    {
        if (_fromId is null || _toId is null)
            return;

        var transit = new ConstantSpeedTransitProfile(1.0);
        var route = RoutePlanner.Find(_fromId, _toId, _graph, transit);
        var points = BuildPoints();
        var mapEdges = _showGraphBox.IsChecked == true ? BuildAllEdges() : new List<StarMapEdge>();

        if (!route.Found)
        {
            _map.SetMap(points, mapEdges);
            _map.SelectedId = _inspectId ?? _fromId;
            _routeEdgeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _routeSummary.Text = FormatRouteParsable(found: false, route: null, waypointIds: []);
            var fallback = EnumerateUndirectedEdges()
                .Select((edge, i) => CreateJump(i + 1, edge, inRoute: false, routeHopIndex: null))
                .OrderBy(j => j.DistanceLy)
                .Select((j, i) => j with { Index = i + 1 })
                .ToList();
            PopulateJumps(_showGraphBox.IsChecked == true ? fallback : []);
            _feedback.SetStatus($"No route {_fromId} → {_toId}");
            _feedback.Flash($"No route from {_fromId} to {_toId} within {MaxRangeLy:0} ly bands.");
            return;
        }

        var routeEdges = new List<StarMapEdge>();
        var routeJumps = new List<JumpInfo>();
        _routeEdgeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < route.WaypointIds.Count - 1; i++)
        {
            var a = route.WaypointIds[i];
            var b = route.WaypointIds[i + 1];
            var edge = _graph.Adjacency[a].First(e =>
                string.Equals(e.To.Value, b, StringComparison.OrdinalIgnoreCase));
            routeEdges.Add(new StarMapEdge { FromId = a, ToId = b, BandTag = edge.BandTag });
            _routeEdgeKeys.Add(EdgeKey(a, b));
            routeJumps.Add(CreateJump(i + 1, edge, inRoute: true, routeHopIndex: i + 1));
        }

        if (_showGraphBox.IsChecked != true)
            mapEdges = routeEdges;
        else
            mapEdges = mapEdges.Concat(routeEdges).ToList();

        _map.SetMap(points, mapEdges);
        _map.SelectedId = _inspectId ?? _toId;

        _routeSummary.Text = FormatRouteParsable(found: true, route, route.WaypointIds);

        if (_showGraphBox.IsChecked == true)
        {
            var all = EnumerateUndirectedEdges()
                .Select((edge, i) => CreateJump(
                    i + 1,
                    edge,
                    inRoute: _routeEdgeKeys.Contains(EdgeKey(edge.From.Value, edge.To.Value)),
                    routeHopIndex: null))
                .OrderByDescending(j => j.InRoute)
                .ThenBy(j => j.DistanceLy)
                .Select((j, i) => j with { Index = i + 1 })
                .ToList();
            // Preserve route hop indices on in-route jumps
            for (var i = 0; i < all.Count; i++)
            {
                if (!all[i].InRoute)
                    continue;
                var match = routeJumps.First(r =>
                    string.Equals(EdgeKey(r.FromId, r.ToId), EdgeKey(all[i].FromId, all[i].ToId), StringComparison.OrdinalIgnoreCase));
                all[i] = all[i] with { RouteHopIndex = match.RouteHopIndex };
            }

            PopulateJumps(all);
        }
        else
        {
            PopulateJumps(routeJumps);
        }

        _feedback.SetStatus($"{route.WaypointIds.Count - 1} hops · {Fmt(route.Accumulation.TotalLy)} ly · cost {Fmt(route.Accumulation.TotalCost)}");
        _feedback.Flash("Route planned — select a jump for parsable detail.");
        RefreshSystemPanel(_inspectId);
    }

    void PopulateJumps(IReadOnlyList<JumpInfo> jumps)
    {
        _jumps.Clear();
        _jumps.AddRange(jumps);
        _suppressJumpSelection = true;
        _jumpList.ItemsSource = null;
        _jumpList.ItemsSource = _jumps;
        _suppressJumpSelection = false;

        if (_jumps.Count == 0)
        {
            _jumpDetail.Text = "No jumps.";
            return;
        }

        var preferred = _jumps.FirstOrDefault(j => j.InRoute) ?? _jumps[0];
        _jumpList.SelectedItem = preferred;
        _jumpDetail.Text = preferred.ToParsable();
    }

    JumpInfo CreateJump(int index, RouteEdge edge, bool inRoute, int? routeHopIndex)
    {
        var from = _catalog.GetRequired(edge.From);
        var to = _catalog.GetRequired(edge.To);
        var band = edge.BandTag ?? "unspecified";
        var costPerLy = band.Equals("long", StringComparison.OrdinalIgnoreCase) ? LongCostPerLy
            : band.Equals("short", StringComparison.OrdinalIgnoreCase) ? ShortCostPerLy
            : edge.DistanceLy > 0 ? edge.Cost / edge.DistanceLy : 0;
        return new JumpInfo(
            Index: index,
            FromId: from.Id.Value,
            ToId: to.Id.Value,
            FromName: from.Name,
            ToName: to.Name,
            DistanceLy: edge.DistanceLy,
            Cost: edge.Cost,
            CostPerLy: costPerLy,
            Band: band,
            BandMaxLy: band.Equals("long", StringComparison.OrdinalIgnoreCase) ? MaxRangeLy : ShortBandMaxLy,
            DurationDays: edge.DistanceLy,
            InRoute: inRoute,
            RouteHopIndex: routeHopIndex);
    }

    IEnumerable<RouteEdge> EnumerateUndirectedEdges()
    {
        foreach (var (_, outgoing) in _graph.Adjacency)
        {
            foreach (var edge in outgoing)
            {
                if (string.Compare(edge.From.Value, edge.To.Value, StringComparison.OrdinalIgnoreCase) > 0)
                    continue;
                yield return edge;
            }
        }
    }

    string FormatRouteParsable(bool found, RouteResult? route, IReadOnlyList<string> waypointIds)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"found={found.ToString().ToLowerInvariant()}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"from={_fromId}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"to={_toId}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"max_hop_ly={Fmt(MaxRangeLy)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"band_short_max_ly={Fmt(ShortBandMaxLy)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"band_short_cost_per_ly={Fmt(ShortCostPerLy)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"band_long_max_ly={Fmt(MaxRangeLy)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"band_long_cost_per_ly={Fmt(LongCostPerLy)}");
        sb.AppendLine("cost_model=RangeBandCostModel.CreatePrototypeCompatible");
        sb.AppendLine("cost_note=long_hops_cost_3x_so_multiple_short_hops_often_cheaper");
        if (!found || route is null)
            return sb.ToString().TrimEnd();

        sb.AppendLine(CultureInfo.InvariantCulture, $"hops={waypointIds.Count - 1}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"waypoints={string.Join(',', waypointIds)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"waypoint_names={string.Join(',', waypointIds.Select(id => _catalog.GetRequired(id).Name))}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"total_ly={Fmt(route.Accumulation.TotalLy)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"total_cost={Fmt(route.Accumulation.TotalCost)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"total_duration_d={Fmt(route.Accumulation.TotalDurationSeconds / 86400.0)}");
        foreach (var kv in route.Accumulation.CountsByBand.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine(CultureInfo.InvariantCulture, $"band_count_{kv.Key}={kv.Value}");
        return sb.ToString().TrimEnd();
    }

    List<StarMapPoint> BuildPoints() =>
        _catalog.All.Select(s => new StarMapPoint
        {
            Id = s.Id.Value,
            Label = s.Name,
            X = s.Coords.X,
            Y = s.Coords.Y
        }).ToList();

    List<StarMapEdge> BuildAllEdges()
    {
        var edges = new List<StarMapEdge>();
        foreach (var edge in EnumerateUndirectedEdges())
        {
            edges.Add(new StarMapEdge
            {
                FromId = edge.From.Value,
                ToId = edge.To.Value,
                BandTag = edge.BandTag
            });
        }

        return edges;
    }

    static string EdgeKey(string a, string b)
    {
        var cmp = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        return cmp <= 0 ? $"{a}|{b}" : $"{b}|{a}";
    }

    static string Fmt(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    static ComboBox MakeSystemCombo(IReadOnlyList<string> ids) =>
        new()
        {
            Width = 200,
            ItemsSource = ids,
            HorizontalAlignment = HorizontalAlignment.Left
        };
}
