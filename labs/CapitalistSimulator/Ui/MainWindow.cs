using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CapitalistSimulator.Cli;
using CapitalistSimulator.Persistence;
using CapitalistSimulator.Sim;
using Novolis.Avalonia.Briefing;
using Novolis.Avalonia.Studio;

namespace CapitalistSimulator.Ui;

internal sealed class MainWindow : Window
{
    private GameWorld _world;
    private CommandProcessor _proc;
    private readonly RunOptions _options;
    private readonly SaveStore _saves = new();
    private readonly StudioChrome _chrome;
    private readonly StudioFeedback _feedback;
    private readonly Canvas _map = new() { Width = 640, Height = 480 };
    private readonly Canvas _interior = new() { Width = 320, Height = 240 };
    private readonly FeedPanel _feed = new() { MinHeight = 140, MaxHeight = 220 };
    private readonly TextBlock _cashChip = CapitalTheme.Mono("$0");
    private readonly TextBlock _dateChip = CapitalTheme.Mono("Day 1");
    private readonly TextBlock _profitChip = CapitalTheme.Mono("$0");
    private readonly TextBlock _shareChip = CapitalTheme.Mono("$0");
    private readonly TextBlock _status = CapitalTheme.Label("", muted: true);
    private readonly TextBlock _coachTitle = CapitalTheme.Title("Getting started", 18);
    private readonly TextBlock _coachBody = CapitalTheme.Label("", muted: false);
    private readonly Button _coachPrimary = CapitalTheme.MakeButton("Advance month", CapitalButtonKind.Primary);
    private readonly TextBlock _firmDetail = CapitalTheme.Mono("", 11);
    private readonly StackPanel _productBars = new() { Spacing = 6 };
    private bool _welcomeShown;
    private readonly ComboBox _cityBox = new() { MinWidth = 140 };
    private readonly ComboBox _buildTypeBox = new() { MinWidth = 180 };
    private readonly ComboBox _speedBox = new();
    private readonly ComboBox _zoomBox = new() { MinWidth = 70 };
    private readonly ListBox _firmList = new() { MinHeight = 120, MaxHeight = 180 };
    private readonly NumericUpDown _priceBox = new() { Minimum = 0.01m, Increment = 0.5m, FormatString = "0.00", Width = 100 };
    private readonly ComboBox _salesProductBox = new() { MinWidth = 140 };
    private readonly ComboBox _salesUnitBox = new() { MinWidth = 120 };
    private FirmId? _selectedFirm;
    private UnitId? _selectedUnit;
    private int? _pendingBuildX;
    private int? _pendingBuildY;
    private bool _runLoop;
    private double _mapCell = 36;
    private readonly DispatcherTimer _timer;

    public MainWindow(GameWorld world, RunOptions options)
    {
        _world = world;
        _proc = new CommandProcessor(world);
        _options = options;
        Title = "Capitalist Simulator";
        Width = 1280;
        Height = 840;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        CapitalTheme.ApplyWindowChrome(this);

        _chrome = StudioChrome.Create();
        _feedback = _chrome.CreateFeedback();
        _chrome.FlashLine.FontFamily = CapitalPalette.BodyFont;
        _chrome.StatusLine.FontFamily = CapitalPalette.BodyFont;
        _chrome.StatusLine.Foreground = CapitalPalette.MutedBrush;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _timer.Tick += async (_, _) => await OnTimerAsync();

        foreach (var c in _world.Cities)
            _cityBox.Items.Add(c.Name);
        _cityBox.SelectedItem = _world.SelectedCityName ?? _world.Cities[0].Name;
        _cityBox.SelectionChanged += (_, _) =>
        {
            if (_cityBox.SelectedItem is string name)
                _proc.Apply(new SelectCityCommand(name));
            RedrawMap();
        };

        var buildIdx = 0;
        var i = 0;
        foreach (var ft in _world.Catalog.FirmTypes.Values)
        {
            _buildTypeBox.Items.Add(new FirmTypeItem(ft.Id, ft.Name));
            if (ft.Id == "retail_super")
                buildIdx = i;
            i++;
        }
        _buildTypeBox.SelectedIndex = buildIdx;

        for (var s = 0; s <= 5; s++)
            _speedBox.Items.Add(s);
        _speedBox.SelectedItem = 0;
        _speedBox.SelectionChanged += (_, _) =>
        {
            if (_speedBox.SelectedItem is int sp)
            {
                _proc.Apply(new SetSpeedCommand(sp));
                _runLoop = sp > 0 && !_world.Paused;
            }
        };

        _zoomBox.Items.Add(new ZoomItem("S", 24));
        _zoomBox.Items.Add(new ZoomItem("M", 36));
        _zoomBox.Items.Add(new ZoomItem("L", 52));
        _zoomBox.SelectedIndex = 1;
        _zoomBox.SelectionChanged += (_, _) =>
        {
            if (_zoomBox.SelectedItem is ZoomItem z)
            {
                _mapCell = z.Cell;
                RedrawMap();
            }
        };

        _coachPrimary.Click += (_, _) => RunCoachPrimary();
        _coachBody.TextWrapping = TextWrapping.Wrap;
        _coachBody.MaxWidth = 520;

        Content = BuildLayout();
        KeyDown += OnKeyDown;

        // Playable first session: stocked supermarket already on the map.
        if (!_world.FirmsOf(_world.Player.Id).Any())
        {
            var firm = StarterBootstrap.EnsureStarterRetail(_world);
            if (firm is not null)
                _selectedFirm = firm.Id;
        }

        RefreshAll();
        _timer.Start();
        _feedback.SetStatus("Your Corner Market is ready — press Advance month.");
        Opened += async (_, _) => await ShowWelcomeIfNeededAsync();
    }

    private Control BuildLayout()
    {
        var primary = new WrapPanel
        {
            Margin = new Thickness(8, 8, 8, 0),
            Children =
            {
                CapitalTheme.MakeButton("Advance month", CapitalButtonKind.Primary).Tap(b => b.Click += async (_, _) => await AdvanceAsync(30)),
                CapitalTheme.MakeButton("Build store").Tap(b => b.Click += (_, _) => TryBuild()),
                CapitalTheme.MakeButton("Fix starter stock").Tap(b => b.Click += (_, _) => FixStarterStock()),
                CapitalTheme.MakeButton("Ops").Tap(b => b.Click += (_, _) => ShowOps()),
                CapitalTheme.MakeButton("Reports").Tap(b => b.Click += (_, _) => ShowReports()),
            },
        };

        var secondary = new WrapPanel
        {
            Margin = new Thickness(8, 4, 8, 0),
            Children =
            {
                CapitalTheme.MakeButton("Bank", CapitalButtonKind.Quiet).Tap(b => b.Click += (_, _) => ShowBank()),
                CapitalTheme.MakeButton("Stock", CapitalButtonKind.Quiet).Tap(b => b.Click += (_, _) => ShowStock()),
                CapitalTheme.MakeButton("Brand", CapitalButtonKind.Quiet).Tap(b => b.Click += (_, _) => ShowBrand()),
                CapitalTheme.MakeButton("HQ", CapitalButtonKind.Quiet).Tap(b => b.Click += (_, _) => ShowHq()),
                CapitalTheme.MakeButton("Auto-Link", CapitalButtonKind.Quiet).Tap(b => b.Click += (_, _) => AutoLinkSelected()),
                CapitalTheme.MakeButton("Demolish", CapitalButtonKind.Quiet).Tap(b => b.Click += (_, _) => DemolishSelected()),
                CapitalTheme.MakeButton("Save", CapitalButtonKind.Quiet).Tap(b => b.Click += (_, _) => { _saves.Save(_world); Flash("Saved"); }),
                CapitalTheme.MakeButton("Load", CapitalButtonKind.Quiet).Tap(b => b.Click += (_, _) => ShowLoad()),
                CapitalTheme.MakeButton("New", CapitalButtonKind.Quiet).Tap(b => b.Click += (_, _) => NewGame()),
                CapitalTheme.MakeButton("Retire", CapitalButtonKind.Danger).Tap(b => b.Click += (_, _) => { _proc.Apply(new RetireCommand()); RefreshAll(); }),
                new TextBlock { Text = "City", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0), Foreground = CapitalPalette.MutedBrush },
                _cityBox,
                new TextBlock { Text = "Build type", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0), Foreground = CapitalPalette.MutedBrush },
                _buildTypeBox,
                new TextBlock { Text = "Speed", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0), Foreground = CapitalPalette.MutedBrush },
                _speedBox,
                new TextBlock { Text = "Zoom", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0), Foreground = CapitalPalette.MutedBrush },
                _zoomBox,
            },
        };

        var toolbar = new StackPanel { Children = { primary, secondary } };

        var metrics = new WrapPanel { Margin = new Thickness(8, 0) };
        metrics.Children.Add(WrapMetric("Cash", _cashChip));
        metrics.Children.Add(WrapMetric("Date", _dateChip));
        metrics.Children.Add(WrapMetric("Mo P&L", _profitChip));
        metrics.Children.Add(WrapMetric("Share", _shareChip));

        _map.PointerPressed += OnMapPointer;
        _map.Background = CapitalPalette.MapFieldBrush;
        var mapHost = new Border
        {
            Background = CapitalPalette.MapFieldBrush,
            BorderBrush = CapitalPalette.PanelRaisedBrush,
            BorderThickness = new Thickness(1),
            Child = new ScrollViewer { Content = _map, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto },
        };

        _firmList.SelectionChanged += (_, _) =>
        {
            if (_firmList.SelectedItem is FirmListItem item)
            {
                _selectedFirm = item.Id;
                PopulateFirmEditors();
                RedrawInterior();
                RefreshFirmDetail();
            }
        };

        var applySales = CapitalTheme.MakeButton("Apply Price/Slot", CapitalButtonKind.Primary);
        applySales.Click += (_, _) => ApplySalesConfig();

        var coachPanel = new Border
        {
            Background = CapitalPalette.PanelRaisedBrush,
            BorderBrush = CapitalPalette.AccentBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 10),
            Child = new StackPanel
            {
                Spacing = 8,
                Children = { _coachTitle, _coachBody, _coachPrimary },
            },
        };

        var left = new StackPanel
        {
            Width = 320,
            Margin = new Thickness(8),
            Children =
            {
                coachPanel,
                CapitalTheme.Section("Firms", _firmList),
                CapitalTheme.Section("Sales slot (price here)", new StackPanel
                {
                    Children =
                    {
                        CapitalTheme.Label("Unit"),
                        _salesUnitBox,
                        CapitalTheme.Label("Product"),
                        _salesProductBox,
                        CapitalTheme.Label("Price"),
                        _priceBox,
                        applySales,
                    },
                }),
                CapitalTheme.Section("Firm detail", _firmDetail),
            },
        };

        _interior.Background = CapitalPalette.PanelRaisedBrush;
        _interior.PointerPressed += OnInteriorPointer;
        var center = new Grid
        {
            RowDefinitions = new RowDefinitions("*,220"),
            Children = { mapHost },
        };
        Grid.SetRow(mapHost, 0);
        var interiorHost = CapitalTheme.Section("Firm interior (click empty cell to place Purchasing)", _interior);
        Grid.SetRow(interiorHost, 1);
        center.Children.Add(interiorHost);

        var right = new StackPanel
        {
            Width = 300,
            Margin = new Thickness(8),
            Children =
            {
                CapitalTheme.Section("News", _feed),
                CapitalTheme.Section("Product P/Q/B · supply/demand", new ScrollViewer
                {
                    MaxHeight = 280,
                    Content = _productBars,
                }),
                CapitalTheme.Section("Status", _status),
            },
        };

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("320,*,320"),
            Margin = new Thickness(4),
        };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(center, 1);
        Grid.SetColumn(right, 2);
        body.Children.Add(left);
        body.Children.Add(center);
        body.Children.Add(right);

        var root = new DockPanel();
        var chromeStrip = new StackPanel
        {
            Children = { _chrome.StatusLine, _chrome.FlashLine },
        };
        var top = new StackPanel { Children = { toolbar, metrics, chromeStrip } };
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        root.Children.Add(body);
        return new Panel
        {
            Children = { root, _chrome.BusyOverlay },
        };
    }

    private static Border WrapMetric(string label, TextBlock value)
    {
        value.FontFamily = CapitalPalette.MonoFont;
        value.FontSize = 14;
        value.FontWeight = FontWeight.SemiBold;
        value.Foreground = CapitalPalette.BodyBrush;
        return new Border
        {
            Background = CapitalPalette.PanelRaisedBrush,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(10, 6),
            Margin = new Thickness(0, 0, 8, 4),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        FontSize = 10,
                        Foreground = CapitalPalette.MutedBrush,
                        FontFamily = CapitalPalette.BodyFont,
                    },
                    value,
                },
            },
        };
    }

    private async Task OnTimerAsync()
    {
        if (!_runLoop || _world.Paused || _world.Speed <= 0) return;
        if (_world.Win.Won || _world.Win.Lost) { _runLoop = false; return; }
        var days = Math.Max(1, _world.Speed);
        await AdvanceAsync(days);
    }

    private async Task AdvanceAsync(int days)
    {
        await Task.Run(() => _proc.Apply(new AdvanceDaysCommand(days)));
        RefreshAll();
        if (_world.Win.Won || _world.Win.Lost)
        {
            _runLoop = false;
            Flash(_world.Win.Message);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            _runLoop = !_runLoop;
            _proc.Apply(new SetPausedCommand(!_runLoop));
            Flash(_runLoop ? "Running" : "Paused");
        }
        else if (e.Key == Key.OemComma) { _zoomBox.SelectedIndex = 0; }
        else if (e.Key == Key.OemPeriod) { _zoomBox.SelectedIndex = 1; }
        else if (e.Key == Key.Oem2) { _zoomBox.SelectedIndex = 2; } // /
    }

    private void OnMapPointer(object? sender, PointerPressedEventArgs e)
    {
        var city = CurrentCity();
        if (city is null) return;
        var p = e.GetPosition(_map);
        var cell = _mapCell;
        var x = (int)(p.X / cell);
        var y = (int)(p.Y / cell);
        if (x < 0 || y < 0 || x >= city.Width || y >= city.Height) return;

        var tile = city.Tiles[x, y];
        if (tile.FirmId is { } fid)
        {
            _selectedFirm = fid;
            SelectFirmInList(fid);
            RedrawInterior();
            RefreshFirmDetail();
            return;
        }

        _pendingBuildX = x;
        _pendingBuildY = y;
        _status.Text = $"Build site ({x},{y}) — click Build or press Build toolbar.";
        Flash($"Site ({x},{y})");
    }

    private void TryBuild()
    {
        if (_pendingBuildX is null || _pendingBuildY is null)
        {
            Flash("Click an empty green map tile first, then Build store");
            return;
        }
        var typeId = _buildTypeBox.SelectedItem switch
        {
            FirmTypeItem item => item.Id,
            string s => s,
            _ => "retail_super",
        };
        var city = _cityBox.SelectedItem as string ?? _world.Cities[0].Name;
        var r = _proc.Apply(new BuildFirmCommand(city, typeId, _pendingBuildX.Value, _pendingBuildY.Value));
        Flash(r.Message);
        if (r.Ok)
        {
            _selectedFirm = _world.FirmsOf(_world.Player.Id).Last().Id;
            _pendingBuildX = _pendingBuildY = null;
            PopulateFirmEditors();
        }
        RefreshAll();
    }

    private void FixStarterStock()
    {
        var firm = _selectedFirm is { } id
            ? _world.FindFirm(id)
            : _world.FirmsOf(_world.Player.Id).FirstOrDefault(f => f.Kind == FirmKind.Retail);
        if (firm is null || !firm.Owner.Equals(_world.Player.Id))
        {
            firm = StarterBootstrap.EnsureStarterRetail(_world);
        }
        else
        {
            StarterBootstrap.ConfigureStockedSupermarket(_world, _proc, firm);
        }
        if (firm is not null)
            _selectedFirm = firm.Id;
        Flash("Starter shelves restocked (bread / milk / soda)");
        RefreshAll();
    }

    private void RunCoachPrimary()
    {
        var step = TutorialCoach.Next(_world);
        switch (step.PrimaryAction)
        {
            case "Advance month":
                _ = AdvanceAsync(30);
                break;
            case "Build store":
                TryBuild();
                break;
            case "Fix starter stock":
                FixStarterStock();
                break;
            case "Reports":
                ShowReports();
                break;
            case "New":
                NewGame();
                break;
            default:
                Flash(step.Title);
                break;
        }
    }

    private void RefreshCoach()
    {
        var step = TutorialCoach.Next(_world);
        _coachTitle.Text = step.Title;
        _coachBody.Text = step.Body;
        _coachPrimary.Content = step.PrimaryAction;
    }

    private async Task ShowWelcomeIfNeededAsync()
    {
        if (_welcomeShown) return;
        _welcomeShown = true;
        var go = CapitalTheme.MakeButton("Got it — Advance month", CapitalButtonKind.Primary);
        var dlg = new Window
        {
            Title = "Capitalist Simulator",
            Width = 520,
            Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 12,
                Children =
                {
                    CapitalTheme.Title("Your first store is open", 22),
                    CapitalTheme.Label(
                        "Corner Market is already built in Metropolis and stocked from the seaport (bread, milk, soda).",
                        muted: false),
                    CapitalTheme.Label(
                        "1) Press Advance month — cash goes out for stock/wages, comes back from shoppers.\n" +
                        "2) Watch the right-hand P/Q/B bars (price, quality, brand) and blue/red supply bars.\n" +
                        "3) Raise prices or expand when the coach says you're profitable.",
                        muted: false),
                    CapitalTheme.Label("Green tiles = yours · Red tiles = rivals · Teal strip = seaport.", muted: true),
                    go,
                },
            },
        };
        CapitalTheme.ApplyWindowChrome(dlg);
        go.Click += async (_, _) =>
        {
            dlg.Close();
            await AdvanceAsync(30);
        };
        await dlg.ShowDialog(this);
    }

    private void DemolishSelected()
    {
        if (_selectedFirm is null) { Flash("No firm selected"); return; }
        Flash(_proc.Apply(new DemolishFirmCommand(_selectedFirm.Value)).Message);
        _selectedFirm = null;
        RefreshAll();
    }

    private void AutoLinkSelected()
    {
        if (_selectedFirm is null) return;
        Flash(_proc.Apply(new AutoLinkCommand(_selectedFirm.Value)).Message);
        RedrawInterior();
        RefreshFirmDetail();
    }

    private void OnInteriorPointer(object? sender, PointerPressedEventArgs e)
    {
        if (_selectedFirm is null) return;
        var firm = _world.FindFirm(_selectedFirm.Value);
        if (firm is null || !firm.Owner.Equals(_world.Player.Id)) return;
        var p = e.GetPosition(_interior);
        const double cell = 48;
        var x = (int)(p.X / cell);
        var y = (int)(p.Y / cell);
        if (x < 0 || y < 0 || x >= firm.LayoutW || y >= firm.LayoutH) return;
        var existing = firm.Units.FirstOrDefault(u => u.X == x && u.Y == y);
        if (existing is not null)
        {
            _selectedUnit = existing.Id;
            RefreshFirmDetail();
            return;
        }
        var kind = firm.Kind switch
        {
            FirmKind.Factory => UnitKind.Manufacturing,
            FirmKind.Farm or FirmKind.Extract => UnitKind.Extract,
            FirmKind.Rd => UnitKind.Rd,
            _ => UnitKind.Purchasing,
        };
        Flash(_proc.Apply(new PlaceUnitCommand(firm.Id, kind, x, y)).Message);
        RedrawInterior();
        PopulateFirmEditors();
        RefreshFirmDetail();
    }

    private void ApplySalesConfig()
    {
        if (_selectedFirm is null) { Flash("Select a firm"); return; }
        if (_salesUnitBox.SelectedItem is not UnitListItem unit) { Flash("Select sales unit"); return; }
        if (_salesProductBox.SelectedItem is not string pid) { Flash("Select product"); return; }
        var price = _priceBox.Value ?? 1;
        var r = _proc.Apply(new ConfigureSalesCommand(_selectedFirm.Value, unit.Id, pid, price));
        // also wire purchasing if present
        var firm = _world.FindFirm(_selectedFirm.Value);
        var buy = firm?.Units.FirstOrDefault(u => u.Kind == UnitKind.Purchasing && u.PurchaseProductId is null)
                  ?? firm?.Units.FirstOrDefault(u => u.Kind == UnitKind.Purchasing);
        if (r.Ok && buy is not null)
            _proc.Apply(new ConfigurePurchasingCommand(_selectedFirm.Value, buy.Id, pid, 120, true, null, false));
        Flash(r.Message);
        RefreshFirmDetail();
        RefreshProductDetail();
    }

    private async void ShowReports()
    {
        var lines = _world.LastMonthSales
            .GroupBy(s => s.ProductId)
            .OrderByDescending(g => g.Sum(x => x.Revenue))
            .Take(20)
            .Select(g => $"{g.Key}: sold {g.Sum(x => x.UnitsSold):N0}  rev ${g.Sum(x => x.Revenue):N0}");
        var corp = $"Cash ${_world.Player.Cash:N0}\nRev ${_world.Player.MonthRevenue:N0}\nExp ${_world.Player.MonthExpense:N0}\nFirms {_world.FirmsOf(_world.Player.Id).Count()}";
        var dlg = new Window
        {
            Title = "Reports",
            Width = 480,
            Height = 520,
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = "CORPORATE\n" + corp + "\n\nPRODUCT SUMMARY\n" + string.Join('\n', lines),
                    FontFamily = CapitalPalette.MonoFont,
                    Margin = new Thickness(16),
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };
        CapitalTheme.ApplyWindowChrome(dlg);
        await dlg.ShowDialog(this);
    }

    private async void ShowHq()
    {
        var fin = new CheckBox { Content = "Finance: auto dividend", IsChecked = _world.Player.Hq.FinanceAutoDividend };
        var mkt = new CheckBox { Content = "Marketing: auto ads", IsChecked = _world.Player.Hq.MarketingAutoAds };
        var imp = new CheckBox { Content = "Import: prefer internal", IsChecked = _world.Player.Hq.ImportPreferInternal };
        var rd = new CheckBox { Content = "R&D: auto start", IsChecked = _world.Player.Hq.RdAutoStart };
        fin.IsCheckedChanged += (_, _) => _proc.Apply(new SetHqFinanceAutoCommand(fin.IsChecked == true));
        mkt.IsCheckedChanged += (_, _) => _proc.Apply(new SetHqMarketingAutoCommand(mkt.IsChecked == true));
        imp.IsCheckedChanged += (_, _) => _proc.Apply(new SetHqImportPreferInternalCommand(imp.IsChecked == true));
        rd.IsCheckedChanged += (_, _) => _proc.Apply(new SetHqRdAutoCommand(rd.IsChecked == true));
        var dlg = new Window
        {
            Title = "Headquarters",
            Width = 360,
            Height = 280,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Children =
                {
                    CapitalTheme.Title("HQ Departments", 18),
                    fin, mkt, imp, rd,
                    CapitalTheme.Label("Build an HQ firm to roleplay presence; toggles apply immediately.", muted: true),
                },
            },
        };
        CapitalTheme.ApplyWindowChrome(dlg);
        await dlg.ShowDialog(this);
    }

    private async void ShowStock()
    {
        var issuerBox = new ComboBox { MinWidth = 200 };
        foreach (var c in _world.Corporations.Where(c => !c.Retired))
            issuerBox.Items.Add(new CorpListItem(c.Id, $"{c.Name} @ ${c.SharePrice:N2}"));
        issuerBox.SelectedIndex = 0;
        var shares = new NumericUpDown { Minimum = 1, Value = 1000, Increment = 100, Width = 120 };
        var buy = CapitalTheme.MakeButton("Buy", CapitalButtonKind.Primary);
        var sell = CapitalTheme.MakeButton("Sell");
        var issue = CapitalTheme.MakeButton("Issue (player)");
        var div = new NumericUpDown { Minimum = 0, Value = _world.Player.DividendPerShare, Increment = 0.01m, FormatString = "0.00", Width = 100 };
        buy.Click += (_, _) =>
        {
            if (issuerBox.SelectedItem is CorpListItem c)
                Flash(_proc.Apply(new BuySharesCommand(c.Id, shares.Value ?? 0)).Message);
            RefreshAll();
        };
        sell.Click += (_, _) =>
        {
            if (issuerBox.SelectedItem is CorpListItem c)
                Flash(_proc.Apply(new SellSharesCommand(c.Id, shares.Value ?? 0)).Message);
            RefreshAll();
        };
        issue.Click += (_, _) =>
        {
            Flash(_proc.Apply(new IssueSharesCommand(shares.Value ?? 0, _world.Player.SharePrice)).Message);
            RefreshAll();
        };
        var setDiv = CapitalTheme.MakeButton("Set dividend");
        setDiv.Click += (_, _) => Flash(_proc.Apply(new SetDividendCommand(div.Value ?? 0)).Message);

        var holdings = string.Join('\n', _world.Holdings.Where(h => h.Owner.Equals(_world.Player.Id))
            .Select(h =>
            {
                var name = _world.FindCorp(h.Issuer)?.Name ?? "?";
                return $"{name}: {h.Shares:N0}";
            }));

        var dlg = new Window
        {
            Title = "Stock Market",
            Width = 420,
            Height = 420,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Children =
                {
                    CapitalTheme.Title("Stock Market", 18),
                    issuerBox,
                    new StackPanel { Orientation = Orientation.Horizontal, Children = { CapitalTheme.Label("Shares"), shares } },
                    new StackPanel { Orientation = Orientation.Horizontal, Children = { buy, sell, issue } },
                    new StackPanel { Orientation = Orientation.Horizontal, Children = { CapitalTheme.Label("Dividend/share"), div, setDiv } },
                    CapitalTheme.Label("Holdings", muted: true),
                    CapitalTheme.Mono(holdings),
                },
            },
        };
        CapitalTheme.ApplyWindowChrome(dlg);
        await dlg.ShowDialog(this);
        RefreshAll();
    }

    private async void ShowBank()
    {
        var amt = new NumericUpDown { Minimum = 1000, Value = 100_000, Increment = 10_000, Width = 140 };
        var borrow = CapitalTheme.MakeButton("Borrow", CapitalButtonKind.Primary);
        var repay = CapitalTheme.MakeButton("Repay");
        borrow.Click += (_, _) => { Flash(_proc.Apply(new BorrowCommand(amt.Value ?? 0)).Message); RefreshAll(); };
        repay.Click += (_, _) => { Flash(_proc.Apply(new RepayCommand(amt.Value ?? 0)).Message); RefreshAll(); };
        var dlg = new Window
        {
            Title = "Bank",
            Width = 360,
            Height = 220,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Children =
                {
                    CapitalTheme.Title("Bank Loans", 18),
                    CapitalTheme.Label($"Outstanding: ${_world.Player.Loans.Sum(l => l.Principal):N0}"),
                    amt,
                    new StackPanel { Orientation = Orientation.Horizontal, Children = { borrow, repay } },
                },
            },
        };
        CapitalTheme.ApplyWindowChrome(dlg);
        await dlg.ShowDialog(this);
    }

    private async void ShowOps()
    {
        if (_selectedFirm is null) { Flash("Select a firm"); return; }
        var firm = _world.FindFirm(_selectedFirm.Value);
        if (firm is null || !firm.Owner.Equals(_world.Player.Id)) { Flash("Not your firm"); return; }

        var unitBox = new ComboBox { MinWidth = 200 };
        foreach (var u in firm.Units)
            unitBox.Items.Add(new UnitListItem(u.Id, $"{u.Kind} @{u.X},{u.Y}"));
        if (unitBox.Items.Count > 0) unitBox.SelectedIndex = 0;

        var productBox = new ComboBox { MinWidth = 160 };
        foreach (var p in _world.Catalog.Products.Values.OrderBy(p => p.Name))
            productBox.Items.Add(p.Id);
        if (productBox.Items.Count > 0) productBox.SelectedIndex = 0;

        var qty = new NumericUpDown { Minimum = 0, Value = 100, Width = 100 };
        var rate = new NumericUpDown { Minimum = 0, Value = 15, Width = 100 };
        var budget = new NumericUpDown { Minimum = 0, Value = 3000, Width = 100 };
        var training = new NumericUpDown { Minimum = 0, Maximum = 1, Value = 0.5m, Increment = 0.05m, Width = 100 };
        var privateLabel = new CheckBox { Content = "Private label" };
        var seaport = new CheckBox { Content = "Buy from seaport", IsChecked = true };

        var applyBuy = CapitalTheme.MakeButton("Purchasing", CapitalButtonKind.Primary);
        applyBuy.Click += (_, _) =>
        {
            if (unitBox.SelectedItem is not UnitListItem u || productBox.SelectedItem is not string pid) return;
            Flash(_proc.Apply(new ConfigurePurchasingCommand(firm.Id, u.Id, pid, qty.Value ?? 0, seaport.IsChecked == true, null, privateLabel.IsChecked == true)).Message);
            RefreshFirmDetail();
        };
        var applyMfg = CapitalTheme.MakeButton("Manufacture");
        applyMfg.Click += (_, _) =>
        {
            if (unitBox.SelectedItem is not UnitListItem u || productBox.SelectedItem is not string pid) return;
            Flash(_proc.Apply(new ConfigureManufacturingCommand(firm.Id, u.Id, pid, rate.Value ?? 0)).Message);
            RefreshFirmDetail();
        };
        var applyAd = CapitalTheme.MakeButton("Advertise");
        applyAd.Click += (_, _) =>
        {
            if (unitBox.SelectedItem is not UnitListItem u || productBox.SelectedItem is not string pid) return;
            var cls = _world.Catalog.Products.TryGetValue(pid, out var p) ? p.Class.ToString() : null;
            Flash(_proc.Apply(new ConfigureAdvertisingCommand(firm.Id, u.Id, pid, cls, budget.Value ?? 0)).Message);
            RefreshFirmDetail();
        };
        var applyExtract = CapitalTheme.MakeButton("Extract");
        applyExtract.Click += (_, _) =>
        {
            if (unitBox.SelectedItem is not UnitListItem u || productBox.SelectedItem is not string pid) return;
            var kind = firm.ExtractKind == ExtractKind.None ? ExtractKind.Crop : firm.ExtractKind;
            Flash(_proc.Apply(new ConfigureExtractCommand(firm.Id, u.Id, kind, pid, qty.Value ?? 40)).Message);
            RefreshFirmDetail();
        };
        var applyRd = CapitalTheme.MakeButton("Start R&D");
        applyRd.Click += (_, _) =>
        {
            if (unitBox.SelectedItem is not UnitListItem u || productBox.SelectedItem is not string pid) return;
            Flash(_proc.Apply(new StartRdCommand(firm.Id, u.Id, pid, 6)).Message);
            RefreshFirmDetail();
        };
        var applyTrain = CapitalTheme.MakeButton("Training");
        applyTrain.Click += (_, _) =>
        {
            if (unitBox.SelectedItem is not UnitListItem u) return;
            Flash(_proc.Apply(new SetTrainingCommand(firm.Id, u.Id, (double)(training.Value ?? 0))).Message);
            RefreshFirmDetail();
        };

        var dlg = new Window
        {
            Title = "Operations",
            Width = 440,
            Height = 480,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Children =
                {
                    CapitalTheme.Title("Unit Ops", 18),
                    unitBox,
                    productBox,
                    new StackPanel { Orientation = Orientation.Horizontal, Children = { CapitalTheme.Label("Qty/Yield"), qty, CapitalTheme.Label("Rate"), rate } },
                    new StackPanel { Orientation = Orientation.Horizontal, Children = { CapitalTheme.Label("Ad $"), budget, CapitalTheme.Label("Train"), training } },
                    privateLabel,
                    seaport,
                    new WrapPanel { Children = { applyBuy, applyMfg, applyAd, applyExtract, applyRd, applyTrain } },
                },
            },
        };
        CapitalTheme.ApplyWindowChrome(dlg);
        await dlg.ShowDialog(this);
    }

    private async void ShowBrand()
    {
        var box = new ComboBox();
        foreach (BrandStrategy s in Enum.GetValues<BrandStrategy>())
            box.Items.Add(s);
        box.SelectedItem = _world.Player.BrandStrategy;
        var apply = CapitalTheme.MakeButton("Apply", CapitalButtonKind.Primary);
        apply.Click += (_, _) =>
        {
            if (box.SelectedItem is BrandStrategy s)
                Flash(_proc.Apply(new SetBrandStrategyCommand(s)).Message);
        };
        var dlg = new Window
        {
            Title = "Brand Strategy",
            Width = 360,
            Height = 200,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Children =
                {
                    CapitalTheme.Title("Brand Strategy", 18),
                    CapitalTheme.Label("Corporate / Range / Unique — affects ad brand keys."),
                    box,
                    apply,
                },
            },
        };
        CapitalTheme.ApplyWindowChrome(dlg);
        await dlg.ShowDialog(this);
    }

    private void RefreshAll()
    {
        _cashChip.Text = $"${_world.Player.Cash:N0}";
        _dateChip.Text = $"D{_world.Day} Y{_world.Year} M{_world.MonthOfYear}";
        var pnl = _world.Player.MonthRevenue - _world.Player.MonthExpense;
        _profitChip.Text = $"{pnl:N0}";
        _profitChip.Foreground = pnl >= 0 ? CapitalPalette.SuccessBrush : CapitalPalette.DangerBrush;
        _shareChip.Text = $"${_world.Player.SharePrice:N2}";
        RefreshFirmList();
        RedrawMap();
        RedrawInterior();
        RefreshFirmDetail();
        RefreshProductDetail();
        RefreshFeed();
        if (_world.Win.Won || _world.Win.Lost)
            _status.Text = _world.Win.Message;
        RefreshCoach();
        if (_selectedFirm is null)
        {
            var first = _world.FirmsOf(_world.Player.Id).FirstOrDefault();
            if (first is not null)
            {
                _selectedFirm = first.Id;
                SelectFirmInList(first.Id);
                PopulateFirmEditors();
                RedrawInterior();
                RefreshFirmDetail();
            }
        }
    }

    private void RefreshFirmList()
    {
        var city = CurrentCity();
        _firmList.Items.Clear();
        foreach (var f in _world.Firms.Where(f => city is null || f.CityId.Equals(city.Id)))
        {
            var owner = _world.FindCorp(f.Owner)?.Name ?? "?";
            var item = new FirmListItem(f.Id, $"{f.Name} [{f.Kind}] — {owner}");
            _firmList.Items.Add(item);
            if (_selectedFirm?.Equals(f.Id) == true)
                _firmList.SelectedItem = item;
        }
    }

    private void SelectFirmInList(FirmId id)
    {
        foreach (var item in _firmList.Items.OfType<FirmListItem>())
        {
            if (item.Id.Equals(id))
            {
                _firmList.SelectedItem = item;
                break;
            }
        }
    }

    private void PopulateFirmEditors()
    {
        _salesUnitBox.Items.Clear();
        _salesProductBox.Items.Clear();
        if (_selectedFirm is null) return;
        var firm = _world.FindFirm(_selectedFirm.Value);
        if (firm is null) return;
        foreach (var u in firm.Units.Where(u => u.Kind == UnitKind.Sales))
            _salesUnitBox.Items.Add(new UnitListItem(u.Id, $"{u.Kind}@{u.X},{u.Y} Lv{u.Level}"));
        if (_salesUnitBox.Items.Count > 0)
            _salesUnitBox.SelectedIndex = 0;

        IEnumerable<ProductDef> products = _world.Catalog.Products.Values;
        if (firm.Kind == FirmKind.Retail && _world.Catalog.FirmTypes.TryGetValue(firm.FirmTypeId, out var type) && type.AllowedClasses.Count > 0)
            products = products.Where(p => type.AllowedClasses.Contains(p.Class));
        foreach (var p in products.OrderBy(p => p.Name))
            _salesProductBox.Items.Add(p.Id);
        if (_salesProductBox.Items.Count > 0)
            _salesProductBox.SelectedIndex = 0;
    }

    private void RefreshFirmDetail()
    {
        if (_selectedFirm is null)
        {
            _firmDetail.Text = "Select a firm on the map or list.";
            return;
        }
        var firm = _world.FindFirm(_selectedFirm.Value);
        if (firm is null) return;
        var inv = string.Join(", ", firm.Inventory.Where(i => i.Quantity > 0).Select(i => $"{i.ProductId}:{i.Quantity:N0}(Q{i.Quality:0.00})"));
        var units = string.Join('\n', firm.Units.Select(u =>
        {
            var extra = u.Kind switch
            {
                UnitKind.Sales => $" sell={u.SalesProductId}@{u.SalesPrice:0.00} sold={u.LastSold:N0} unmet={u.LastUnmetDemand:N0}",
                UnitKind.Purchasing => $" buy={u.PurchaseProductId} qty={u.PurchaseQtyTarget}",
                UnitKind.Manufacturing => $" recipe={u.RecipeOutputId} rate={u.ProductionRate}",
                UnitKind.Advertising => $" ad={u.AdProductId} ${u.AdBudget:N0}",
                UnitKind.Extract => $" {u.ExtractKind}:{u.ExtractProductId} y={u.ExtractYield}",
                UnitKind.Rd => $" rd={u.RdTargetProductId} m={u.RdMonthsRemaining}",
                _ => "",
            };
            var mark = _selectedUnit?.Equals(u.Id) == true ? "*" : " ";
            return $"{mark}{u.Kind} ({u.X},{u.Y}) Lv{u.Level}{extra}";
        }));
        _firmDetail.Text = $"{firm.Name}\nLinks {firm.Links.Count}\nInv: {inv}\n{units}";
    }

    private void RefreshProductDetail()
    {
        _productBars.Children.Clear();
        var slots = _world.FirmsOf(_world.Player.Id)
            .SelectMany(f => f.Units
                .Where(u => u.Kind == UnitKind.Sales && u.SalesProductId is not null)
                .Select(u => (Firm: f, Unit: u)))
            .Take(8)
            .ToList();

        if (slots.Count == 0)
        {
            _productBars.Children.Add(CapitalTheme.Label("No retail/sales slots configured.", muted: true));
            return;
        }

        foreach (var (firm, unit) in slots)
        {
            var pid = unit.SalesProductId!;
            _world.Catalog.Products.TryGetValue(pid, out var prod);
            var lot = _world.FindLot(firm, pid);
            var stock = lot?.Quantity ?? 0;
            var quality = lot?.Quality ?? 0.5;
            var brandKey = _world.Player.BrandStrategy switch
            {
                BrandStrategy.Corporate => "corporate",
                BrandStrategy.Range => prod?.Class.ToString() ?? "corporate",
                BrandStrategy.Unique => pid,
                _ => "corporate",
            };
            _world.Player.Brands.TryGetValue(brandKey, out var brand);
            brand ??= _world.Player.Brands.GetValueOrDefault("corporate") ?? new BrandState();
            var basePrice = prod?.BasePrice ?? Math.Max(0.01m, unit.SalesPrice);
            var priceAttr = Math.Clamp(1.4 - (double)(unit.SalesPrice / basePrice), 0.05, 1.2);
            var demand = unit.LastSold + unit.LastUnmetDemand;
            var supplyFrac = demand <= 0 ? (stock > 0 ? 1 : 0) : Math.Clamp((double)(stock + unit.LastSold) / (double)Math.Max(1m, demand + stock), 0, 1);
            var unmetFrac = demand <= 0 ? 0 : Math.Clamp((double)unit.LastUnmetDemand / (double)demand, 0, 1);

            var block = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 8) };
            block.Children.Add(new TextBlock
            {
                Text = $"{pid} @ ${unit.SalesPrice:0.00}  sold {unit.LastSold:N0}",
                FontFamily = CapitalPalette.MonoFont,
                FontSize = 11,
                Foreground = CapitalPalette.BodyBrush,
            });
            block.Children.Add(MetricBar("Price attr", priceAttr / 1.2, Color.Parse("#d4a017")));
            block.Children.Add(MetricBar("Quality", quality, Color.Parse("#6ecf8e")));
            block.Children.Add(MetricBar("Brand", brand.Awareness * 0.6 + brand.Loyalty * 0.4, Color.Parse("#e08a4a")));
            block.Children.Add(SupplyDemandBar(supplyFrac, unmetFrac));
            _productBars.Children.Add(block);
        }
    }

    private static Control MetricBar(string label, double value01, Color fill)
    {
        value01 = Math.Clamp(value01, 0, 1);
        var track = new Border
        {
            Height = 10,
            Background = new SolidColorBrush(Color.Parse("#243040")),
            CornerRadius = new CornerRadius(2),
            Child = new Grid
            {
                Children =
                {
                    new Border
                    {
                        Width = Math.Max(2, 220 * value01),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Background = new SolidColorBrush(fill),
                        CornerRadius = new CornerRadius(2),
                    },
                },
            },
        };
        return new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = $"{label} {(value01 * 100):0}%",
                    FontSize = 10,
                    Foreground = CapitalPalette.MutedBrush,
                    FontFamily = CapitalPalette.BodyFont,
                },
                track,
            },
        };
    }

    private static Control SupplyDemandBar(double supplyFrac, double unmetFrac)
    {
        // Cap2-style: blue stock / red unmet
        var width = 220.0;
        var blueW = Math.Max(0, width * supplyFrac * (1 - unmetFrac * 0.5));
        var redW = Math.Max(0, width * unmetFrac);
        var row = new Canvas { Width = width, Height = 12 };
        row.Children.Add(new Rectangle
        {
            Width = width,
            Height = 12,
            Fill = new SolidColorBrush(Color.Parse("#243040")),
        });
        var blue = new Rectangle
        {
            Width = blueW,
            Height = 12,
            Fill = new SolidColorBrush(Color.Parse("#3a7ca5")),
        };
        Canvas.SetLeft(blue, 0);
        row.Children.Add(blue);
        var red = new Rectangle
        {
            Width = redW,
            Height = 12,
            Fill = new SolidColorBrush(Color.Parse("#c45c4a")),
        };
        Canvas.SetLeft(red, Math.Min(width - redW, blueW));
        row.Children.Add(red);
        return new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = "Supply (blue) / unmet demand (red)",
                    FontSize = 10,
                    Foreground = CapitalPalette.MutedBrush,
                    FontFamily = CapitalPalette.BodyFont,
                },
                row,
            },
        };
    }

    private void NewGame()
    {
        _runLoop = false;
        _welcomeShown = false;
        var world = WorldFactory.Create(
            _options.Scenario,
            _options.StartingCash,
            _options.AiCount,
            _options.AiAggressiveness,
            Environment.TickCount);
        var firm = StarterBootstrap.EnsureStarterRetail(world);
        ReplaceWorld(world);
        if (firm is not null)
            _selectedFirm = firm.Id;
        Flash("New game — starter store ready");
        _ = ShowWelcomeIfNeededAsync();
    }

    private void ShowLoad()
    {
        var names = _saves.List();
        if (names.Count == 0)
        {
            Flash("No saves found");
            return;
        }
        var box = new ComboBox { MinWidth = 200 };
        foreach (var n in names)
            box.Items.Add(n);
        box.SelectedIndex = 0;
        var loadBtn = CapitalTheme.MakeButton("Load", CapitalButtonKind.Primary);
        var dlg = new Window
        {
            Title = "Load game",
            Width = 320,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Children = { CapitalTheme.Title("Saves", 16), box, loadBtn },
            },
        };
        CapitalTheme.ApplyWindowChrome(dlg);
        loadBtn.Click += (_, _) =>
        {
            if (box.SelectedItem is not string name) return;
            var loaded = _saves.Load(name);
            if (loaded is null)
            {
                Flash("Load failed");
                return;
            }
            ReplaceWorld(loaded);
            dlg.Close();
            Flash($"Loaded {name}");
        };
        _ = dlg.ShowDialog(this);
    }

    private void ReplaceWorld(GameWorld world)
    {
        _world = world;
        _proc = new CommandProcessor(world);
        _selectedFirm = null;
        _selectedUnit = null;
        _pendingBuildX = _pendingBuildY = null;
        _cityBox.Items.Clear();
        foreach (var c in _world.Cities)
            _cityBox.Items.Add(c.Name);
        _cityBox.SelectedItem = _world.SelectedCityName ?? _world.Cities[0].Name;
        RefreshAll();
    }

    private void RefreshFeed()
    {
        var lines = _world.News.TakeLast(30)
            .Select(n => new FeedLine("news", n.Text, $"d{n.Day}"))
            .ToList();
        _feed.SetLines(lines);
    }

    private void RedrawMap()
    {
        _map.Children.Clear();
        var city = CurrentCity();
        if (city is null) return;
        var cell = _mapCell;
        _map.Width = city.Width * cell;
        _map.Height = city.Height * cell;
        for (var y = 0; y < city.Height; y++)
        for (var x = 0; x < city.Width; x++)
        {
            var tile = city.Tiles[x, y];
            var color = tile.Kind switch
            {
                TileKind.Road => CapitalPalette.Road,
                TileKind.Seaport => CapitalPalette.Seaport,
                TileKind.Bank => CapitalPalette.Bank,
                TileKind.StockExchange => Color.Parse("#403050"),
                TileKind.Blocked => Color.Parse("#1a1a1a"),
                _ => CapitalPalette.MapField,
            };
            if (tile.FirmId is { } fid)
            {
                var firm = _world.FindFirm(fid);
                color = firm is not null && firm.Owner.Equals(_world.Player.Id)
                    ? CapitalPalette.PlayerFirm
                    : CapitalPalette.AiFirm;
            }
            var rect = new Rectangle
            {
                Width = cell - 2,
                Height = cell - 2,
                Fill = new SolidColorBrush(color),
                Stroke = _selectedFirm is { } sf && tile.FirmId?.Equals(sf) == true
                    ? CapitalPalette.AccentBrush
                    : new SolidColorBrush(Color.Parse("#243040")),
                StrokeThickness = 1,
            };
            Canvas.SetLeft(rect, x * cell);
            Canvas.SetTop(rect, y * cell);
            _map.Children.Add(rect);
            if (tile.FirmId is not null)
            {
                var firm = _world.FindFirm(tile.FirmId.Value);
                if (firm is not null && firm.TileX == x && firm.TileY == y)
                {
                    var label = new TextBlock
                    {
                        Text = firm.Kind.ToString()[..Math.Min(3, firm.Kind.ToString().Length)],
                        FontSize = 9,
                        Foreground = CapitalPalette.BodyBrush,
                        FontFamily = CapitalPalette.MonoFont,
                    };
                    Canvas.SetLeft(label, x * cell + 4);
                    Canvas.SetTop(label, y * cell + 10);
                    _map.Children.Add(label);
                }
            }
        }
    }

    private void RedrawInterior()
    {
        _interior.Children.Clear();
        if (_selectedFirm is null) return;
        var firm = _world.FindFirm(_selectedFirm.Value);
        if (firm is null) return;
        const double cell = 48;
        _interior.Width = firm.LayoutW * cell;
        _interior.Height = firm.LayoutH * cell;
        for (var y = 0; y < firm.LayoutH; y++)
        for (var x = 0; x < firm.LayoutW; x++)
        {
            var unit = firm.Units.FirstOrDefault(u => u.X == x && u.Y == y);
            var rect = new Rectangle
            {
                Width = cell - 4,
                Height = cell - 4,
                Fill = new SolidColorBrush(unit is null ? CapitalPalette.Panel : Color.Parse("#2a4050")),
                Stroke = unit is not null && _selectedUnit?.Equals(unit.Id) == true
                    ? CapitalPalette.AccentBrush
                    : CapitalPalette.MutedBrush,
                StrokeThickness = 1,
            };
            Canvas.SetLeft(rect, x * cell);
            Canvas.SetTop(rect, y * cell);
            _interior.Children.Add(rect);
            if (unit is not null)
            {
                var t = new TextBlock
                {
                    Text = unit.Kind.ToString()[..Math.Min(3, unit.Kind.ToString().Length)],
                    FontSize = 10,
                    Foreground = CapitalPalette.BodyBrush,
                    FontFamily = CapitalPalette.MonoFont,
                };
                Canvas.SetLeft(t, x * cell + 6);
                Canvas.SetTop(t, y * cell + 14);
                _interior.Children.Add(t);
            }
        }
        // draw links
        foreach (var (fromId, toId) in firm.Links)
        {
            var a = firm.Units.FirstOrDefault(u => u.Id.Equals(fromId));
            var b = firm.Units.FirstOrDefault(u => u.Id.Equals(toId));
            if (a is null || b is null) continue;
            var line = new Line
            {
                StartPoint = new Point(a.X * cell + cell / 2, a.Y * cell + cell / 2),
                EndPoint = new Point(b.X * cell + cell / 2, b.Y * cell + cell / 2),
                Stroke = CapitalPalette.AccentBrush,
                StrokeThickness = 1.5,
                Opacity = 0.7,
            };
            _interior.Children.Add(line);
        }
    }

    private City? CurrentCity()
    {
        var name = _cityBox.SelectedItem as string ?? _world.SelectedCityName;
        return name is null ? _world.Cities.FirstOrDefault() : _world.FindCityByName(name);
    }

    private void Flash(string msg)
    {
        _feedback.Flash(msg);
        _status.Text = msg;
    }

    private sealed record FirmListItem(FirmId Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record UnitListItem(UnitId Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record CorpListItem(CorpId Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record ZoomItem(string Label, double Cell)
    {
        public override string ToString() => Label;
    }

    private sealed record FirmTypeItem(string Id, string Name)
    {
        public override string ToString() => Name;
    }
}

internal static class ControlTapExtensions
{
    public static T Tap<T>(this T control, Action<T> configure)
    {
        configure(control);
        return control;
    }
}
