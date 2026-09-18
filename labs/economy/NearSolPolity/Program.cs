using System.Diagnostics;
using NearSolPolity;
using Novolis.Economy;
using Novolis.Economy.Agents;
using Novolis.Economy.Finance;
using Novolis.Economy.Logistics;
using Novolis.Economy.Markets;
using Novolis.Economy.Simulation;
using Spectre.Console;

var headless = TryParseHeadless(args, out var runHours);
if (!headless && (Console.IsOutputRedirected || Console.IsInputRedirected))
{
  headless = !args.Any(a => a.Equals("--live", StringComparison.OrdinalIgnoreCase));
  if (!DurationArg.TryParse(args.FirstOrDefault(a => !a.StartsWith('-')), out runHours))
  {
    runHours = 500;
  }
}

var (sim, ids) = PolityWorld.Create();
var agents = NearSolAgents.Create(sim, ids);
var credits = new CreditCirculation(new EconomySimulationCreditSource(sim));
credits.SetFirmNames(ids.Firms);
credits.SetSkuIds(ids.Ore, ids.Parts, ids.Goods, ids.Fuel);
var log = new Queue<string>();
var eventCursor = sim.State.Events.Count;
var running = true;
var hoursPerPulse = 1;
var pulseMs = 280;
var openingLiquid = credits.LiquidStock;

if (headless)
{
  var sw = Stopwatch.StartNew();
  hoursPerPulse = 24;
  var remaining = runHours;
  var lastPct = -1;
  while (remaining > 0)
  {
    var step = (int)Math.Min(hoursPerPulse, remaining);
    hoursPerPulse = step;
    await PulseAsync(captureLog: false);
    remaining -= step;

    var done = runHours - remaining;
    var pct = (int)(done * 100 / runHours);
    if (pct != lastPct && pct % 10 == 0)
    {
      lastPct = pct;
      Console.Error.WriteLine($"… {DurationArg.Format(done)} / {DurationArg.Format(runHours)} ({pct}%)");
    }
  }

  sw.Stop();
  credits.CaptureFinalMilestone();
  HeadlessReport.Write(sim, ids, credits, openingLiquid, runHours, sw.Elapsed, agents);
  return;
}

AnsiConsole.Write(new FigletText("Near-Sol").Color(Color.SteelBlue1));
AnsiConsole.MarkupLine("[grey]Tycoon slice — economic agents + hub book + finance.[/]");
AnsiConsole.MarkupLine(
  $"[grey]Travel:[/] {AstroEconomyBridge.CruiseDaysPerLy:0.##} d/ly  " +
  $"(Sol→α Cen ~{AstroEconomyBridge.TransitDays(4.4):0.#}d)  " +
  "[grey]SKU:[/] Raw / Capital / Final / Energy");
AnsiConsole.MarkupLine(
  "[grey]Keys:[/] [bold]1[/]=½×  [bold]2[/]=1×  [bold]3[/]=4×  [bold]4[/]=16×  " +
  "[bold]5[/]=64×  [bold]6[/]=Warp  [bold]Space[/]=pause  [bold]Q[/]=quit");
AnsiConsole.MarkupLine("[grey]Headless:[/] [bold]--headless 100d[/]");
Note($"Catalog online — {ids.Bridge.Hubs.Count} hubs, liquid {openingLiquid:0}");

await AnsiConsole.Live(Dashboard.Build(sim, ids, agents, credits, log, running, hoursPerPulse, pulseMs))
  .AutoClear(false)
  .Overflow(VerticalOverflow.Ellipsis)
  .StartAsync(async ctx =>
  {
    while (true)
    {
      ctx.UpdateTarget(Dashboard.Build(sim, ids, agents, credits, log, running, hoursPerPulse, pulseMs));

      while (Console.KeyAvailable)
      {
        var key = Console.ReadKey(intercept: true);
        if (!HandleKey(key))
        {
          Note("Clearing docking clamps.");
          ctx.UpdateTarget(Dashboard.Build(sim, ids, agents, credits, log, running, hoursPerPulse, pulseMs));
          return;
        }
      }

      if (running)
      {
        await PulseAsync(captureLog: true);
      }

      await Task.Delay(pulseMs);
    }
  });

PrintSummary();
return;

bool TryParseHeadless(string[] argv, out long hours)
{
  hours = 500;
  for (var i = 0; i < argv.Length; i++)
  {
    var a = argv[i];
    if (a.Equals("--headless", StringComparison.OrdinalIgnoreCase)
        || a.Equals("--report", StringComparison.OrdinalIgnoreCase)
        || a.Equals("headless", StringComparison.OrdinalIgnoreCase))
    {
      if (i + 1 < argv.Length && DurationArg.TryParse(argv[i + 1], out hours))
      {
        return true;
      }

      hours = 500;
      return true;
    }

    if (a.StartsWith("--headless=", StringComparison.OrdinalIgnoreCase))
    {
      return DurationArg.TryParse(a["--headless=".Length..], out hours);
    }
  }

  return false;
}

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

async Task PulseAsync(bool captureLog)
{
  var before = sim.State.Events.Count;
  for (var h = 0; h < hoursPerPulse; h++)
  {
    var ctx = new AgentContext(sim, new DeterministicRandom(sim.State.Seed ^ (ulong)sim.State.Clock.HourIndex));
    // Snapshot — venture agent may append carriers mid-tick for the *next* hour.
    AgentScheduler.TickAll(agents.PulseOrder.ToArray(), ctx);
    agents.RebuildPulse();
    await sim.AdvanceAsync(SimulationDuration.FromHours(1));
  }

  credits.ObserveAfterPulse(before);
  if (captureLog)
  {
    Capture(before);
  }
  else
  {
    eventCursor = sim.State.Events.Count;
  }
}

void Capture(int before)
{
  var events = sim.State.Events;
  for (var i = Math.Max(before, eventCursor); i < events.Count; i++)
  {
    var line = events[i] switch
    {
      ShipmentDeparted e => $"departed ×{e.Quantity.Value:0}",
      ShipmentDelivered e => $"delivered ×{e.Quantity.Value:0}",
      ShipmentPlanFailed e => $"plan failed: {e.Reason}",
      HubOrderFilled e => $"book fill {PolityWorld.SkuLabel(e.ProductId, ids)} ×{e.Quantity.Value:0}",
      HubOrderPosted => null,
      GoodsSold e when e.Revenue.Amount >= 0.5m => $"sold ×{e.Quantity.Value:0} → {e.Revenue.Amount:0.##}",
      GoodsSoldInterFirm e => $"B2B ×{e.Quantity.Value:0}",
      HouseholdCreditsIssued e when e.Amount.Amount >= 0.5m => $"hh credits {e.Amount.Amount:0}",
      ProcurementFilled e => $"import ×{e.Quantity.Value:0}",
      ExportFilled e => $"export Raw ×{e.Quantity.Value:0} ${e.Revenue.Amount:0}",
      BatchProduced e => $"produced {PolityWorld.SkuLabel(e.ProductId, ids)} ×{e.Quantity.Value:0}",
      LoanOriginated e => $"loan {e.Principal.Amount:0}",
      OwnershipChanged e => $"own {e.Fraction:0.##}",
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
  while (log.Count > 12)
  {
    log.Dequeue();
  }
}

void PrintSummary()
{
  AnsiConsole.MarkupLine(
    "[green]Done.[/] Hash [bold]{0:X16}[/]  liquid {1:0} (open {2:0})  fills {3}  wages→hh {4:0}  imports {5:0}",
    sim.State.Hash,
    credits.LiquidStock,
    openingLiquid,
    credits.BookFills,
    credits.WagesDistributed,
    credits.ImportSpend);
}
