using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Logistics;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;
using Spectre.Console;
using TrampFreighterPlay;

AnsiConsole.Write(new FigletText("MV Independent").Color(Color.SteelBlue1));
AnsiConsole.MarkupLine("[grey]Tramp freighter dogfood — hubs, fuel, tolls, crew. No Astro coupling.[/]");
AnsiConsole.MarkupLine("[grey]Keys:[/] [bold]1[/]=1h  [bold]D[/]=24h  [bold]J[/]=job F→Core  [bold]R[/]=return parts  " +
                       "[bold]X[/]=rim (fail)  [bold]B[/]=bunker  [bold]S[/]=sell ore  [bold]Q[/]=quit");

var (sim, ids) = TrampScenario.Create();
TrampScenario.SeedStarterCargo(sim, ids);
var log = new Queue<string>();
void Note(string msg)
{
  log.Enqueue($"h{sim.State.Clock.HourIndex}: {msg}");
  while (log.Count > 8)
  {
    log.Dequeue();
  }
}

Note("Boarded at Frontier Outpost with ore and bunker contracts.");

if (Console.IsOutputRedirected || Console.IsInputRedirected)
{
  AnsiConsole.MarkupLine("[yellow]Non-interactive console — printing one snapshot then exiting.[/]");
  AnsiConsole.Write(BuildView());
  await Tick(1);
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
      var key = await WaitKeyAsync();
      switch (char.ToUpperInvariant(key))
      {
        case 'Q':
          Note("Clearing docking clamps. Fair winds.");
          ctx.UpdateTarget(BuildView());
          return;
        case '1':
          await Tick(1);
          break;
        case 'D':
          await Tick(24);
          Note("Day watch complete.");
          break;
        case 'J':
          EnqueueOutbound();
          break;
        case 'R':
          EnqueueReturn();
          break;
        case 'X':
          sim.Enqueue(new PlanShipment(
            ids.Tramp, ids.HubFrontier.Value, ids.HubRim.Value, ids.Ore, Quantity.From(5m), ids.Hull.Value));
          Note("Filed Sparse Rim flight plan (expect tank-range refusal).");
          await Tick(2);
          break;
        case 'B':
          sim.Enqueue(new PlaceProcurementOrder(
            ids.Tramp, ids.LocWay, ids.Fuel, Quantity.From(20m), Money.From(1.5m)));
          Note("Ordered waystation bunker top-up.");
          await Tick(1);
          break;
        case 'S':
          sim.Enqueue(new SetRetailPrice(ids.Tramp, ids.CoreFacility, ids.Ore, Money.From(9m)));
          Note("Posted ore ask at Core Port.");
          await Tick(12);
          break;
      }
    }
  });

AnsiConsole.MarkupLine("[green]Done.[/] Hash [bold]{0:X16}[/]", sim.State.Hash);
return;

async Task Tick(int hours)
{
  var before = sim.State.Events.Count;
  await sim.AdvanceAsync(SimulationDuration.FromHours(hours));
  foreach (var ev in sim.State.Events.Skip(before).TakeLast(12))
  {
    switch (ev)
    {
      case ShipmentDeparted d:
        Note($"Departed with {d.Quantity.Value:0} cargo.");
        break;
      case ShipmentLegStarted:
        Note("Entered jump corridor.");
        break;
      case ShipmentHubArrived:
        Note("Arrived hub — dwell / bunker.");
        break;
      case ShipmentDelivered d:
        Note($"Delivered {d.Quantity.Value:0} — working capital unlocked.");
        break;
      case ShipmentPlanFailed f:
        Note($"Plan failed: {f.Reason}");
        break;
      case FuelBunkered b:
        Note($"Bunkered {b.Quantity.Value:0.##} fuel.");
        break;
      case TransportTollPaid t:
        Note($"Paid toll {t.Amount.Amount:0.##}");
        break;
      case GoodsSold g:
        Note($"Sold {g.Quantity.Value:0} ore for {g.Revenue.Amount:0.##}");
        break;
    }
  }
}

void EnqueueOutbound()
{
  var ore = sim.State.World.Inventory.GetQuantity(new InventoryKey(ids.Tramp, ids.LocFrontier, ids.Ore));
  if (ore.Value < 1m)
  {
    sim.Enqueue(new PlaceProcurementOrder(
      ids.Tramp, ids.LocFrontier, ids.Ore, Quantity.From(20m), Money.From(2m)));
    Note("Bought speculative ore at Frontier.");
  }

  var qty = Quantity.From(Math.Min(25m, Math.Max(1m, ore.Value > 0 ? ore.Value : 20m)));
  sim.Enqueue(new PlanShipment(
    ids.Tramp, ids.HubFrontier.Value, ids.HubCore.Value, ids.Ore, qty, ids.Hull.Value));
  Note($"Filed job: Frontier → Core ({qty.Value:0} ore).");
}

void EnqueueReturn()
{
  sim.Enqueue(new PlaceProcurementOrder(
    ids.Tramp, ids.LocCore, ids.Parts, Quantity.From(12m), Money.From(3m)));
  sim.Enqueue(new PlanShipment(
    ids.Tramp, ids.HubCore.Value, ids.HubFrontier.Value, ids.Parts, Quantity.From(12m), ids.Hull.Value));
  Note("Return leg: Core → Frontier with parts.");
}

Table BuildView()
{
  var world = sim.State.World;
  var ledger = world.Ledgers[ids.Tramp];
  var stats = world.TransportStats;
  var ship = world.Shipments.FirstOrDefault(s => !s.IsLegacy);

  var root = new Table().Border(TableBorder.Rounded).Expand();
  root.AddColumn(new TableColumn("[steelblue1]MV Independent[/]").Width(42));
  root.AddColumn(new TableColumn("[grey]Lane board[/]"));

  var left = new Table().HideHeaders().Border(TableBorder.None);
  left.AddColumn("k");
  left.AddColumn("v");
  left.AddRow("Hour", $"[bold]{sim.State.Clock.HourIndex}[/] (day {sim.State.Clock.Date.DayIndex})");
  left.AddRow("Cash", $"[green]{ledger.Cash.Amount:0.00}[/]");
  left.AddRow("Fuel opex", $"{ledger.Balance(AccountRole.TransportFuelExpense).Amount:0.##}");
  left.AddRow("Toll opex", $"{ledger.Balance(AccountRole.TransportTollExpense).Amount:0.##}");
  left.AddRow("Wages", $"{ledger.Balance(AccountRole.WageExpense).Amount:0.##}");
  left.AddRow("Revenue", $"{ledger.Balance(AccountRole.Revenue).Amount:0.##}");
  left.AddRow("Delivered", $"{stats.CargoDelivered.Value:0}  burned {stats.FuelBurned.Value:0.##}");
  left.AddRow("Crew h", $"{stats.CrewLaborHours:0.##}  fails {stats.FailedPlans}");
  left.AddRow("Stock F", StockLine(ids.LocFrontier));
  left.AddRow("Stock W", StockLine(ids.LocWay));
  left.AddRow("Stock C", StockLine(ids.LocCore));

  if (ship is null)
  {
    left.AddRow("Hull", "[grey]docked / idle[/]");
  }
  else
  {
    var hubName = world.Hubs.TryGetValue(ship.CurrentHubId, out var h) ? h.Name : "?";
    left.AddRow("Hull", $"[yellow]{ship.Phase}[/] @ {hubName}");
    left.AddRow("Leg", $"{ship.LegIndex}/{ship.Itinerary.LegCount}  seg {ship.SegmentHoursRemaining}h");
    left.AddRow("Onboard fuel", $"{ship.OnboardFuel.Value:0.##}");
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

  var right = new Rows(map, journal);
  root.AddRow(left, right);
  return root;

  string StockLine(InventoryLocationId loc) =>
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

static async Task<char> WaitKeyAsync()
{
  while (!Console.KeyAvailable)
  {
    await Task.Delay(40);
  }

  var key = Console.ReadKey(intercept: true);
  return key.KeyChar == '\0' ? (char)key.Key : key.KeyChar;
}
