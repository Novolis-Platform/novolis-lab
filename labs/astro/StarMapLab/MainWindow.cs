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

/// <summary>One selectable FTL hop with machine-readable fields.</summary>
internal sealed record JumpInfo(
    int Index,
    string FromId,
    string ToId,
    string FromName,
    string ToName,
    double DistanceLy,
    double Cost,
    double CostPerLy,
    string Band,
    double BandMaxLy,
    double DurationDays,
    bool InRoute,
    int? RouteHopIndex)
{
    public override string ToString()
    {
        var route = InRoute ? $" route#{RouteHopIndex}" : "";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"#{Index} {FromId} → {ToId}  {DistanceLy:0.##} ly  cost {Cost:0.##}  [{Band}]{route}");
    }

    public string ToParsable()
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"index={Index}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"from={FromId}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"to={ToId}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"from_name={FromName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"to_name={ToName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"distance_ly={DistanceLy.ToString("0.####", CultureInfo.InvariantCulture)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"cost={Cost.ToString("0.####", CultureInfo.InvariantCulture)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"cost_per_ly={CostPerLy.ToString("0.####", CultureInfo.InvariantCulture)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"band={Band}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"band_max_ly={BandMaxLy.ToString("0.####", CultureInfo.InvariantCulture)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"duration_d={DurationDays.ToString("0.####", CultureInfo.InvariantCulture)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"in_route={InRoute.ToString().ToLowerInvariant()}");
        if (RouteHopIndex is { } hop)
            sb.AppendLine(CultureInfo.InvariantCulture, $"route_hop_index={hop}");
        sb.AppendLine("cost_note=long_band_is_3x_short_so_one_long_jump_is_often_worse_than_several_short");
        return sb.ToString().TrimEnd();
    }
}

file static class DemoCatalog
{
    public static StarCatalog Create()
    {
        // Real nearby stars ≤ ~20.5 ly. Galactic XYZ in ly from Johnston (2022):
        // +X galactic center, +Y galactic rotation, +Z galactic north. Primaries only.
        var catalog = new StarCatalog();
        catalog.Add(new StarSystem("sol", "Sol", new StarCoords(0, 0, 0), SpectralClass.G, ["home"]));
        catalog.Add(new StarSystem("proxima-centauri", "Proxima Centauri", new StarCoords(2.945, -3.056, -0.143), SpectralClass.M, ["nearest"]));
        catalog.Add(new StarSystem("alpha-centauri", "Alpha Centauri", new StarCoords(3.126, -3.047, -0.052), SpectralClass.G, ["binary"]));
        catalog.Add(new StarSystem("barnards-star", "Barnard's Star", new StarCoords(4.958, 2.98, 1.449), SpectralClass.M));
        catalog.Add(new StarSystem("wolf-359", "Wolf 359", new StarCoords(-1.916, -3.938, 6.522), SpectralClass.M));
        catalog.Add(new StarSystem("lalande-21185", "Lalande 21185", new StarCoords(-3.439, -0.308, 7.553), SpectralClass.M));
        catalog.Add(new StarSystem("sirius", "Sirius", new StarCoords(-5.809, -6.28, -1.338), SpectralClass.A, ["bright"]));
        catalog.Add(new StarSystem("luyten-726-8", "Luyten 726-8", new StarCoords(-2.171, 0.171, -8.544), SpectralClass.M));
        catalog.Add(new StarSystem("ross-154", "Ross 154", new StarCoords(9.365, 1.873, -1.733), SpectralClass.M));
        catalog.Add(new StarSystem("ross-248", "Ross 248", new StarCoords(-3.37, 9.265, -3.003), SpectralClass.M));
        catalog.Add(new StarSystem("epsilon-eridani", "Epsilon Eridani", new StarCoords(-6.753, -1.917, -7.811), SpectralClass.K, ["planet-host"]));
        catalog.Add(new StarSystem("lacaille-9352", "Lacaille 9352", new StarCoords(4.352, 0.388, -9.794), SpectralClass.M));
        catalog.Add(new StarSystem("ross-128", "Ross 128", new StarCoords(0.014, -5.577, 9.49), SpectralClass.M, ["planet-host"]));
        catalog.Add(new StarSystem("ez-aquarii", "EZ Aquarii", new StarCoords(4.123, 4.432, -9.315), SpectralClass.M));
        catalog.Add(new StarSystem("61-cygni", "61 Cygni", new StarCoords(1.516, 11.244, -1.156), SpectralClass.K, ["binary"]));
        catalog.Add(new StarSystem("procyon", "Procyon", new StarCoords(-9.27, -6.183, 2.577), SpectralClass.F, ["bright"]));
        catalog.Add(new StarSystem("struve-2398", "Struve 2398", new StarCoords(0.13, 10.478, 4.716), SpectralClass.M, ["binary"]));
        catalog.Add(new StarSystem("groombridge-34", "Groombridge 34", new StarCoords(-4.949, 9.849, -3.677), SpectralClass.M));
        catalog.Add(new StarSystem("dx-cancri", "DX Cancri", new StarCoords(-9.428, -2.885, 6.262), SpectralClass.M));
        catalog.Add(new StarSystem("epsilon-indi", "Epsilon Indi", new StarCoords(7.259, -3.203, -8.825), SpectralClass.K, ["planet-host"]));
        catalog.Add(new StarSystem("tau-ceti", "Tau Ceti", new StarCoords(-3.369, 0.408, -11.412), SpectralClass.G, ["candidate"]));
        catalog.Add(new StarSystem("luyten-372-58", "GJ 1061", new StarCoords(-2.249, -6.869, -9.559), SpectralClass.M));
        catalog.Add(new StarSystem("yz-ceti", "YZ Ceti", new StarCoords(-2.04, 1.192, -11.89), SpectralClass.M));
        catalog.Add(new StarSystem("luytens-star", "Luyten's Star", new StarCoords(-10.262, -6.499, 2.224), SpectralClass.M));
        catalog.Add(new StarSystem("teegardens-star", "Teegarden's Star", new StarCoords(-9.391, 3.369, -7.526), SpectralClass.M, ["planet-host"]));
        catalog.Add(new StarSystem("kapteyns-star", "Kapteyn's Star", new StarCoords(-3.46, -9.787, -7.542), SpectralClass.M));
        catalog.Add(new StarSystem("lacaille-8760", "Lacaille 8760", new StarCoords(9.252, 0.631, -9.036), SpectralClass.K));
        catalog.Add(new StarSystem("kruger-60", "Kruger 60", new StarCoords(-3.316, 12.651, -0.001), SpectralClass.M));
        catalog.Add(new StarSystem("denis-j1048", "DENIS J1048-3956", new StarCoords(1.904, -12.468, 3.872), SpectralClass.M));
        catalog.Add(new StarSystem("ross-614", "Ross 614", new StarCoords(-11.202, -7.255, -1.447), SpectralClass.M));
        catalog.Add(new StarSystem("wolf-1061", "Wolf 1061", new StarCoords(12.845, 0.752, 5.643), SpectralClass.M, ["planet-host"]));
        catalog.Add(new StarSystem("van-maanens-star", "van Maanen's Star", new StarCoords(-3.996, 6.424, -11.865), SpectralClass.Unknown, ["white-dwarf"]));
        catalog.Add(new StarSystem("wolf-424", "Wolf 424", new StarCoords(1.448, -4.264, 13.375), SpectralClass.M));
        catalog.Add(new StarSystem("hd-225213", "GJ 1", new StarCoords(3.311, -0.977, -13.748), SpectralClass.M));
        catalog.Add(new StarSystem("tz-arietis", "TZ Arietis", new StarCoords(-8.48, 5.369, -10.573), SpectralClass.M));
        catalog.Add(new StarSystem("bd-68-946", "BD+68 946", new StarCoords(-1.883, 12.448, 7.856), SpectralClass.M));
        catalog.Add(new StarSystem("cd-46-11540", "CD-46 11540", new StarCoords(14.102, -4.31, -1.752), SpectralClass.M));
        catalog.Add(new StarSystem("lhs-292", "LHS 292", new StarCoords(-1.749, -11.036, 9.813), SpectralClass.M));
        catalog.Add(new StarSystem("luyten-145-141", "Luyten 145-141", new StarCoords(6.625, -13.574, -0.753), SpectralClass.Unknown, ["white-dwarf"]));
        catalog.Add(new StarSystem("v1581-cygni", "V1581 Cygni", new StarCoords(2.905, 14.752, 2.25), SpectralClass.M));
        catalog.Add(new StarSystem("ross-780", "Gliese 876", new StarCoords(4.743, 6.071, -13.148), SpectralClass.M, ["planet-host"]));
        catalog.Add(new StarSystem("luyten-143-23", "Luyten 143-23", new StarCoords(4.928, -14.958, -0.555), SpectralClass.M));
        catalog.Add(new StarSystem("lhs-2", "LHS 2", new StarCoords(-0.258, 5.986, -14.627), SpectralClass.M));
        catalog.Add(new StarSystem("groombridge-1618", "Groombridge 1618", new StarCoords(-9.453, 2.38, 12.543), SpectralClass.K));
        catalog.Add(new StarSystem("lalande-21258", "Lalande 21258", new StarCoords(-7.099, 1.444, 14.263), SpectralClass.M));
        catalog.Add(new StarSystem("ad-leonis", "AD Leonis", new StarCoords(-7.549, -5.577, 13.197), SpectralClass.M));
        catalog.Add(new StarSystem("hd-204961", "Gliese 832", new StarCoords(10.984, -2.101, -11.722), SpectralClass.M, ["planet-host"]));
        catalog.Add(new StarSystem("cd-44-11909", "CD-44 11909", new StarCoords(15.741, -3.929, -1.887), SpectralClass.M));
        catalog.Add(new StarSystem("omicron2-eridani", "40 Eridani", new StarCoords(-12.033, -4.56, -10.071), SpectralClass.K));
        catalog.Add(new StarSystem("ev-lacertae", "EV Lacertae", new StarCoords(-2.954, 15.775, -3.726), SpectralClass.M));
        catalog.Add(new StarSystem("70-ophiuchi", "70 Ophiuchi", new StarCoords(14.2, 8.163, 3.293), SpectralClass.K, ["binary"]));
        catalog.Add(new StarSystem("altair", "Altair", new StarCoords(11.115, 12.234, -2.591), SpectralClass.A, ["bright"]));
        catalog.Add(new StarSystem("ei-cancri", "EI Cancri", new StarCoords(-11.979, -6.249, 9.985), SpectralClass.M));
        catalog.Add(new StarSystem("g-99-49", "G 99-49", new StarCoords(-15.208, -6.951, -2.991), SpectralClass.M));
        catalog.Add(new StarSystem("lhs-2459", "LHS 2459", new StarCoords(-8.104, 10.814, 10.538), SpectralClass.M));
        catalog.Add(new StarSystem("wisea-j1540", "WISEA J1540-5101", new StarCoords(14.713, -9.182, 1.032), SpectralClass.M));
        catalog.Add(new StarSystem("lhs-1723", "LHS 1723", new StarCoords(-13.929, -6.923, -8.087), SpectralClass.M));
        catalog.Add(new StarSystem("wolf-498", "Wolf 498", new StarCoords(5.298, -0.785, 16.898), SpectralClass.M));
        catalog.Add(new StarSystem("stein-2051", "Stein 2051", new StarCoords(-15.151, 9.43, 2.29), SpectralClass.M));
        catalog.Add(new StarSystem("wolf-294", "Wolf 294", new StarCoords(-17.56, -0.901, 4.756), SpectralClass.M));
        catalog.Add(new StarSystem("lp-816-60", "LP 816-60", new StarCoords(13.106, 7.617, -10.308), SpectralClass.M));
        catalog.Add(new StarSystem("wisea-j1835", "WISEA J1835+3259", new StarCoords(8.406, 15.577, 5.562), SpectralClass.M));
        catalog.Add(new StarSystem("wolf-1453", "Wolf 1453", new StarCoords(-15.64, -7.947, -6.194), SpectralClass.M));
        catalog.Add(new StarSystem("hd-42581", "Gliese 229", new StarCoords(-11.786, -13.374, -5.944), SpectralClass.M));
        catalog.Add(new StarSystem("sigma-draconis", "Sigma Draconis", new StarCoords(-3.419, 17.107, 7.005), SpectralClass.G));
        catalog.Add(new StarSystem("ross-47", "Ross 47", new StarCoords(-18.114, -4.418, -3.024), SpectralClass.M));
        catalog.Add(new StarSystem("lalande-27173", "Gliese 570", new StarCoords(15.009, -5.99, 10.366), SpectralClass.K));
        catalog.Add(new StarSystem("luyten-205-128", "Luyten 205-128", new StarCoords(16.899, -7.771, -4.794), SpectralClass.M));
        catalog.Add(new StarSystem("luyten-347-14", "Luyten 347-14", new StarCoords(17.464, -2.343, -7.809), SpectralClass.M));
        catalog.Add(new StarSystem("lalande-46650", "Lalande 46650", new StarCoords(-0.655, 10.511, -16.143), SpectralClass.M));
        catalog.Add(new StarSystem("wolf-1055", "Wolf 1055", new StarCoords(14.658, 12.495, -1.105), SpectralClass.M));
        catalog.Add(new StarSystem("cd-40-9712", "CD-40 9712", new StarCoords(16.764, -8.662, 4.055), SpectralClass.M));
        catalog.Add(new StarSystem("eta-cassiopeiae", "Eta Cassiopeiae", new StarCoords(-10.379, 16.216, -1.703), SpectralClass.G));
        catalog.Add(new StarSystem("luyten-722-22", "Luyten 722-22", new StarCoords(0.49, 4.595, -18.815), SpectralClass.M));
        catalog.Add(new StarSystem("36-ophiuchi", "36 Ophiuchi", new StarCoords(19.266, -0.579, 2.325), SpectralClass.K));
        catalog.Add(new StarSystem("ross-882", "Ross 882", new StarCoords(-15.397, -11.128, 4.546), SpectralClass.M));
        catalog.Add(new StarSystem("hd-191408", "HD 191408", new StarCoords(16.752, 1.534, -10.078), SpectralClass.K));
        catalog.Add(new StarSystem("82-eridani", "82 Eridani", new StarCoords(-3.626, -10.382, -16.351), SpectralClass.G));
        catalog.Add(new StarSystem("ross-986", "Ross 986", new StarCoords(-18.559, 0.339, 6.721), SpectralClass.M));
        catalog.Add(new StarSystem("delta-pavonis", "Delta Pavonis", new StarCoords(14.509, -8.456, -10.664), SpectralClass.G));
        catalog.Add(new StarSystem("hd-191849", "HD 191849", new StarCoords(16.798, -1.527, -10.943), SpectralClass.M));
        catalog.Add(new StarSystem("lhs-455", "LHS 455", new StarCoords(-3.453, 17.077, 10.339), SpectralClass.Unknown, ["white-dwarf"]));
        catalog.Add(new StarSystem("wolf-1481", "Wolf 1481", new StarCoords(13.836, -5.472, 13.95), SpectralClass.M));
        catalog.Add(new StarSystem("eq-pegasi", "EQ Pegasi", new StarCoords(-2.363, 15.667, -12.895), SpectralClass.M));
        return catalog;
    }
}

file static class DemoOverlay
{
    public static CatalogOverlay Create()
    {
        var overlay = new CatalogOverlay();
        overlay.Bind(new OverlayEntry("Home", "sol", new Dictionary<string, string> { ["role"] = "origin" }));
        overlay.Bind(new OverlayEntry("Nearest Neighbor", "proxima-centauri", new Dictionary<string, string> { ["role"] = "scout" }));
        overlay.Bind(new OverlayEntry("Bright Beacon", "sirius", new Dictionary<string, string> { ["role"] = "nav-fix" }));
        overlay.Bind(new OverlayEntry("Frontier Gate", "epsilon-eridani", new Dictionary<string, string> { ["role"] = "staging" }));
        overlay.Bind(new OverlayEntry("Candidate World", "tau-ceti", new Dictionary<string, string> { ["role"] = "survey" }));
        overlay.Bind(new OverlayEntry("Cygnus Waypoint", "61-cygni", new Dictionary<string, string> { ["role"] = "resupply" }));
        overlay.Bind(new OverlayEntry("Aquila Beacon", "altair", new Dictionary<string, string> { ["role"] = "destination" }));
        overlay.Bind(new OverlayEntry("Dragon's Tail", "sigma-draconis", new Dictionary<string, string> { ["role"] = "outbound" }));
        return overlay;
    }
}
