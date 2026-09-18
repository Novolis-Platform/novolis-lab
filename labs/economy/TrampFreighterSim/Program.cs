using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Logistics;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;
using Spectre.Console;
using TrampFreighterSim;

AnsiConsole.Write(new FigletText("Tramp Sim").Color(Color.SteelBlue1));
AnsiConsole.MarkupLine("[grey]Observational tramp circuit — autopilot + variable speed. No Astro coupling.[/]");
AnsiConsole.MarkupLine(
  "[grey]Keys while running:[/] [bold]1[/]=½×  [bold]2[/]=1×  [bold]3[/]=4×  [bold]4[/]=16×  " +
  "[bold]5[/]=64×  [bold]6[/]=Warp  [bold]Space[/]=pause/resume  [bold]Q[/]=quit");

var (sim, ids) = TrampWorld.Create();
var pilot = new TrampAutopilot(sim, ids, ids.HullClass);
var log = new Queue<string>();
var eventCursor = sim.State.Events.Count;
var running = true;
var hoursPerPulse = 1;
var pulseMs = 280;
Note("Autopilot online — Frontier circuit.");

if (Console.IsOutputRedirected || Console.IsInputRedirected)
{
  AnsiConsole.MarkupLine("[yellow]Non-interactive — advancing 200h then exiting.[/]");
  for (var i = 0; i < 200; i++)
  {
    await PulseAsync();
  }

  AnsiConsole.Write(BuildView());
  AnsiConsole.MarkupLine("[green]Done.[/] Hash [bold]{0:X16}[/]", sim.State.Hash);
  return;
}

await AnsiConsole.Live(BuildView())
  .AutoClear(false)
  .Overflow(VerticalOverflow.Ellipsis)
  .StartAsync(async ctx =>
  {
    while (true)
    {
      ctx.UpdateTarget(BuildView());

      // Drain key buffer for speed changes without blocking the sim pulse.
      while (Console.KeyAvailable)
      {
        var key = Console.ReadKey(intercept: true);
        if (!HandleKey(key))
        {
          Note("Clearing docking clamps.");
          ctx.UpdateTarget(BuildView());
          return;
        }
      }

      if (running)
      {
        await PulseAsync();
      }

      await Task.Delay(pulseMs);
    }
  });

AnsiConsole.MarkupLine("[green]Done.[/] Hash [bold]{0:X16}[/]", sim.State.Hash);
return;

bool HandleKey(ConsoleKeyInfo key)
{
  switch (key.Key)
  {
    case ConsoleKey.Q:
      return false;
    case ConsoleKey.Spacebar:
      running = !running;
      Note(running ? "resumed" : "paused");
      break;
    case ConsoleKey.D1:
      SetSpeed(1, 550, "½×");
      break;
    case ConsoleKey.D2:
      SetSpeed(1, 280, "1×");
      break;
    case ConsoleKey.D3:
      SetSpeed(1, 70, "4×");
      break;
    case ConsoleKey.D4:
      SetSpeed(4, 50, "16×");
      break;
    case ConsoleKey.D5:
      SetSpeed(16, 40, "64×");
      break;
    case ConsoleKey.D6:
      SetSpeed(48, 30, "Warp");
      break;
  }

  return true;
}

void SetSpeed(int hours, int ms, string label)
{
  hoursPerPulse = hours;
  pulseMs = ms;
  running = true;
  Note($"speed {label} ({hours}h / pulse)");
}

async Task PulseAsync()
{
  pilot.Tick();
  var before = sim.State.Events.Count;
  await sim.AdvanceAsync(SimulationDuration.FromHours(hoursPerPulse));
  Capture(before);
}

void Capture(int before)
{
  var events = sim.State.Events;
  for (var i = Math.Max(before, eventCursor); i < events.Count; i++)
  {
    var line = events[i] switch
    {
      ShipmentDeparted e => $"departed ×{e.Quantity.Value:0}",
      ShipmentLegStarted => "entered corridor",
      ShipmentHubArrived => "hub arrival / dwell",
      ShipmentDelivered e => $"delivered ×{e.Quantity.Value:0}",
      ShipmentPlanFailed e => $"plan failed: {e.Reason}",
      FuelBunkered e => $"bunkered {e.Quantity.Value:0.##}",
      TransportTollPaid e => $"toll {e.Amount.Amount:0.##}",
      GoodsSold e => $"sold ×{e.Quantity.Value:0} → {e.Revenue.Amount:0.##}",
      ProcurementFilled e => $"procured ×{e.Quantity.Value:0}",
      _ => null,
    };
    if (line is not null)
    {
      Note(line);
    }
  }

  eventCursor = events.Count;
}

void Note(string msg)
{
  log.Enqueue($"h{sim.State.Clock.HourIndex}: {msg}");
  while (log.Count > 10)
  {
    log.Dequeue();
  }
}

Table BuildView()
{
  var world = sim.State.World;
  var ledger = world.Ledgers[ids.Tramp];
  var stats = world.TransportStats;
  var ship = world.Shipments.FirstOrDefault(s => !s.IsLegacy);

  var root = new Table().Border(TableBorder.Rounded).Expand();
  root.AddColumn(new TableColumn("[steelblue1]MV Independent[/]").Width(44));
  root.AddColumn(new TableColumn("[grey]Lane board[/]"));

  var left = new Table().HideHeaders().Border(TableBorder.None);
  left.AddColumn("k");
  left.AddColumn("v");
  left.AddRow("Hour", $"[bold]{sim.State.Clock.HourIndex}[/] (day {sim.State.Clock.Date.DayIndex})");
  left.AddRow("Speed", running
    ? $"[green]{hoursPerPulse}h[/] / {pulseMs}ms"
    : "[yellow]PAUSED[/]");
  left.AddRow("Cash", $"[green]{ledger.Cash.Amount:0.00}[/]");
  left.AddRow("Fuel opex", $"{ledger.Balance(AccountRole.TransportFuelExpense).Amount:0.##}");
  left.AddRow("Tolls", $"{ledger.Balance(AccountRole.TransportTollExpense).Amount:0.##}");
  left.AddRow("Wages", $"{ledger.Balance(AccountRole.WageExpense).Amount:0.##}");
  // Revenue is credit-normal (stored negative on this ledger).
  left.AddRow("Revenue", $"{Math.Abs(ledger.Balance(AccountRole.Revenue).Amount):0.##}");
  left.AddRow("Delivered", $"{stats.CargoDelivered.Value:0}  burn {stats.FuelBurned.Value:0.##}");
  left.AddRow("Crew / fails", $"{stats.CrewLaborHours:0.##} / {stats.FailedPlans}");
  left.AddRow("Autopilot", $"[yellow]{Markup.Escape(pilot.LastDecision)}[/]");
  left.AddRow("Eval", $"[grey]{Markup.Escape(pilot.LastEval)}[/]");
  left.AddRow("Stock F", Stock(ids.LocFrontier));
  left.AddRow("Stock W", $"fuel {Qty(ids.LocWay, ids.Fuel):0}");
  left.AddRow("Stock C", Stock(ids.LocCore));

  if (ship is null)
  {
    left.AddRow("Hull", "[grey]docked / idle[/]");
  }
  else
  {
    var hubName = world.Hubs.TryGetValue(ship.CurrentHubId, out var h) ? h.Name : "?";
    left.AddRow("Hull", $"[aqua]{ship.Phase}[/] @ {hubName}");
    left.AddRow("Leg", $"{ship.LegIndex}/{ship.Itinerary.LegCount}  seg {ship.SegmentHoursRemaining}h");
    left.AddRow("Fuel onboard", $"{ship.OnboardFuel.Value:0.##}");
  }

  var map = new Panel(BuildMap(ship))
  {
    Header = new PanelHeader(" Starport network "),
    Border = BoxBorder.Square,
  };
  var journal = new Panel(string.Join('\n', log.Reverse().Select(l => $"[grey]{Markup.Escape(l)}[/]")))
  {
    Header = new PanelHeader(" Log "),
    Border = BoxBorder.Rounded,
  };

  root.AddRow(left, new Rows(map, journal));
  return root;

  string Stock(InventoryLocationId loc) =>
    $"ore {Qty(loc, ids.Ore):0}  parts {Qty(loc, ids.Parts):0}  fuel {Qty(loc, ids.Fuel):0}";

  decimal Qty(InventoryLocationId loc, ProductId p) =>
    world.Inventory.GetQuantity(new InventoryKey(ids.Tramp, loc, p)).Value;
}

string BuildMap(ActiveShipment? ship)
{
  var marker = ship is null
    ? "·"
    : ship.Phase switch
    {
      ShipmentPhase.Underway => "◆",
      ShipmentPhase.WaitingBerth => "…",
      _ => "●",
    };

  string At(TransportHubId hub) =>
    ship is not null && ship.CurrentHubId.Equals(hub) ? $"[yellow]{marker}[/]" : " ";

  return
    $"""
     {At(ids.HubRim)}[grey]Sparse Rim[/]  (over-range spoke)
            │ 12h / tank-scarce
     {At(ids.HubFrontier)}[bold]Frontier[/] ──4h── {At(ids.HubWay)}[bold]Waystation[/] ──5h── {At(ids.HubCore)}[bold]Core Port[/]
     """;
}
