using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Logistics;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;

namespace EconomyBoard;

internal sealed class MainWindow : Window
{
  private readonly EconomySimulation _sim;
  private readonly ChainScenario.Ids _ids;
  private readonly TextBlock _hourText = MakeMono(18);
  private readonly TextBlock _cashText = MakeMono(16);
  private readonly TextBlock _ledgerText = MakeMono(13);
  private readonly TextBlock _invText = MakeMono(13);
  private readonly TextBlock _shipText = MakeMono(13);
  private readonly TextBlock _eventText = MakeMono(12);
  private readonly Canvas _flow = new() { Width = 720, Height = 220, Background = new SolidColorBrush(Color.Parse("#1a1f2e")) };
  private readonly List<string> _recent = [];
  private bool _running;
  private int _eventCursor;

  public MainWindow()
  {
    Title = "EconomyBoard — commodity chain dogfood";
    Width = 980;
    Height = 720;
    WindowStartupLocation = WindowStartupLocation.CenterScreen;
    Background = new SolidColorBrush(Color.Parse("#0f1219"));

    (_sim, _ids) = ChainScenario.Create();
    _eventCursor = _sim.State.Events.Count;

    var tick1 = new Button { Content = "+1 hour", Margin = new Thickness(4), Padding = new Thickness(12, 6) };
    var tick24 = new Button { Content = "+24 hours", Margin = new Thickness(4), Padding = new Thickness(12, 6) };
    var run = new Button { Content = "Run / Pause", Margin = new Thickness(4), Padding = new Thickness(12, 6) };
    tick1.Click += async (_, _) => await AdvanceAsync(1);
    tick24.Click += async (_, _) => await AdvanceAsync(24);
    run.Click += async (_, _) => await ToggleRunAsync();

    var controls = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Children = { tick1, tick24, run },
      Margin = new Thickness(12, 8),
    };

    var header = new StackPanel
    {
      Margin = new Thickness(16, 12, 16, 4),
      Children =
      {
        new TextBlock
        {
          Text = "Integrated Co — Raw → Mid → Fin",
          FontSize = 22,
          FontWeight = FontWeight.SemiBold,
          Foreground = Brushes.White,
        },
        _hourText,
        _cashText,
      },
    };

    var columns = new Grid
    {
      Margin = new Thickness(16),
      ColumnDefinitions = new ColumnDefinitions("*,*"),
      RowDefinitions = new RowDefinitions("Auto,*,Auto"),
    };
    Grid.SetColumnSpan(_flow, 2);
    columns.Children.Add(_flow);

    var left = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#171c2a")),
      CornerRadius = new CornerRadius(8),
      Padding = new Thickness(12),
      Margin = new Thickness(0, 8, 8, 8),
      Child = new StackPanel
      {
        Children =
        {
          Section("Inventory"),
          _invText,
          Section("Shipments"),
          _shipText,
        },
      },
    };
    Grid.SetRow(left, 1);
    Grid.SetColumn(left, 0);
    columns.Children.Add(left);

    var right = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#171c2a")),
      CornerRadius = new CornerRadius(8),
      Padding = new Thickness(12),
      Margin = new Thickness(8, 8, 0, 8),
      Child = new StackPanel
      {
        Children =
        {
          Section("Ledger"),
          _ledgerText,
          Section("Recent events"),
          _eventText,
        },
      },
    };
    Grid.SetRow(right, 1);
    Grid.SetColumn(right, 1);
    columns.Children.Add(right);

    Content = new DockPanel
    {
      LastChildFill = true,
      Children =
      {
        Place(controls, Dock.Bottom),
        Place(header, Dock.Top),
        columns,
      },
    };

    Refresh();
  }

  private async Task ToggleRunAsync()
  {
    _running = !_running;
    while (_running)
    {
      await AdvanceAsync(1);
      await Task.Delay(35);
    }
  }

  private async Task AdvanceAsync(int hours)
  {
    await _sim.AdvanceAsync(SimulationDuration.FromHours(hours));
    CaptureEvents();
    Refresh();
  }

  private void CaptureEvents()
  {
    var events = _sim.State.Events;
    for (; _eventCursor < events.Count; _eventCursor++)
    {
      var line = events[_eventCursor] switch
      {
        BatchProduced e => $"produced {e.ProductId.Value.ToString()[..8]}… ×{e.Quantity.Value:0}",
        GoodsSold e => $"sold ×{e.Quantity.Value:0} @ {e.UnitPrice.Amount:0.##} → {e.Revenue.Amount:0.##}",
        ShipmentDeparted e => $"ship out ×{e.Quantity.Value:0}",
        ShipmentDelivered e => $"ship in ×{e.Quantity.Value:0}",
        WagesPaid e => $"wages {e.Amount.Amount:0.##}",
        _ => null,
      };
      if (line is null)
      {
        continue;
      }

      _recent.Insert(0, $"h{_sim.State.Clock.HourIndex}: {line}");
      if (_recent.Count > 14)
      {
        _recent.RemoveAt(_recent.Count - 1);
      }
    }
  }

  private void Refresh()
  {
    var world = _sim.State.World;
    var ledger = world.Ledgers[_ids.Firm];
    _hourText.Text = $"Simulation hour {_sim.State.Clock.HourIndex}  ·  day {_sim.State.Clock.Date.DayIndex}  ·  hash {_sim.State.Hash:X8}";
    _cashText.Text = $"Cash {ledger.Cash.Amount:0.00}   revenue {ledger.Balance(AccountRole.Revenue).Amount:0.##}   COGS {ledger.Balance(AccountRole.CostOfGoodsSold).Amount:0.##}   wages {ledger.Balance(AccountRole.WageExpense).Amount:0.##}";

    decimal Qty(InventoryLocationId loc, ProductId p) =>
      world.Inventory.GetQuantity(new InventoryKey(_ids.Firm, loc, p)).Value;

    _invText.Text =
      $"Storage  raw {Qty(_ids.Storage, _ids.Raw):0}  mid {Qty(_ids.Storage, _ids.Mid):0}  fin {Qty(_ids.Storage, _ids.Fin):0}\n" +
      $"Retail   fin {Qty(_ids.Retail, _ids.Fin):0}";

    _shipText.Text = world.Shipments.Count == 0
      ? "(none in transit)"
      : string.Join('\n', world.Shipments.Select(s =>
        $"{s.ProductId.Value.ToString()[..8]}… ×{s.Quantity.Value:0}  rem {s.HoursRemaining}h  {s.Status}"));

    _ledgerText.Text = string.Join('\n', Enum.GetValues<AccountRole>()
      .Select(r => $"{r,-22} {ledger.Balance(r).Amount,10:0.00}"));

    _eventText.Text = _recent.Count == 0 ? "(run the clock)" : string.Join('\n', _recent);

    DrawFlow(Qty(_ids.Storage, _ids.Raw), Qty(_ids.Storage, _ids.Mid), Qty(_ids.Storage, _ids.Fin), Qty(_ids.Retail, _ids.Fin));
  }

  private void DrawFlow(decimal raw, decimal mid, decimal finStore, decimal finRetail)
  {
    _flow.Children.Clear();
    DrawNode(40, 70, "RAW", raw, Color.Parse("#6b8cae"));
    DrawArrow(150, 100);
    DrawNode(190, 70, "MID", mid, Color.Parse("#7a9e6b"));
    DrawArrow(300, 100);
    DrawNode(340, 70, "FIN", finStore, Color.Parse("#c4a35a"));
    DrawArrow(450, 100);
    DrawNode(490, 70, "SHELF", finRetail, Color.Parse("#c47a5a"));

    var inFlight = _sim.State.World.Shipments.Count(s => s.Status == ShipmentStatus.InTransit);
    if (inFlight > 0)
    {
      var pulse = (_sim.State.Clock.HourIndex % 8) / 8.0;
      var x = 450 + pulse * 40;
      _flow.Children.Add(new Ellipse
      {
        Width = 14,
        Height = 14,
        Fill = new SolidColorBrush(Color.Parse("#f0e6a8")),
        [Canvas.LeftProperty] = x,
        [Canvas.TopProperty] = 96,
      });
    }

    _flow.Children.Add(new TextBlock
    {
      Text = "auto-restock FreightRoute (1h)",
      Foreground = new SolidColorBrush(Color.Parse("#8a93a8")),
      FontSize = 12,
      [Canvas.LeftProperty] = 430,
      [Canvas.TopProperty] = 140,
    });
  }

  private void DrawNode(double x, double y, string label, decimal qty, Color color)
  {
    _flow.Children.Add(new Rectangle
    {
      Width = 100,
      Height = 72,
      RadiusX = 10,
      RadiusY = 10,
      Fill = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
      Stroke = new SolidColorBrush(color),
      StrokeThickness = 2,
      [Canvas.LeftProperty] = x,
      [Canvas.TopProperty] = y,
    });
    _flow.Children.Add(new TextBlock
    {
      Text = label,
      FontWeight = FontWeight.Bold,
      Foreground = Brushes.White,
      [Canvas.LeftProperty] = x + 14,
      [Canvas.TopProperty] = y + 12,
    });
    var barW = Math.Clamp((double)qty / 5.0, 0, 72);
    _flow.Children.Add(new Rectangle
    {
      Width = barW,
      Height = 10,
      Fill = new SolidColorBrush(color),
      [Canvas.LeftProperty] = x + 14,
      [Canvas.TopProperty] = y + 38,
    });
    _flow.Children.Add(new TextBlock
    {
      Text = qty.ToString("0"),
      Foreground = new SolidColorBrush(Color.Parse("#c8d0e0")),
      FontSize = 12,
      [Canvas.LeftProperty] = x + 14,
      [Canvas.TopProperty] = y + 50,
    });
  }

  private void DrawArrow(double x, double y)
  {
    _flow.Children.Add(new Line
    {
      StartPoint = new Point(x, y),
      EndPoint = new Point(x + 30, y),
      Stroke = new SolidColorBrush(Color.Parse("#5a6478")),
      StrokeThickness = 2,
    });
  }

  private static TextBlock Section(string title) => new()
  {
    Text = title,
    FontWeight = FontWeight.SemiBold,
    Foreground = new SolidColorBrush(Color.Parse("#9eb0d0")),
    Margin = new Thickness(0, 0, 0, 6),
  };

  private static TextBlock MakeMono(double size) => new()
  {
    FontFamily = new FontFamily("Consolas,Courier New,monospace"),
    FontSize = size,
    Foreground = new SolidColorBrush(Color.Parse("#dce3f0")),
    TextWrapping = TextWrapping.Wrap,
  };

  private static Control Place(Control child, Dock dock)
  {
    DockPanel.SetDock(child, dock);
    return child;
  }
}
