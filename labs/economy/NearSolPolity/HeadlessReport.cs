using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Core.Extensions;
using Novolis.Economy.Finance;
using Novolis.Economy.Logistics;
using Novolis.Economy.Markets;
using Novolis.Economy.Population;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;
using Spectre.Console;

namespace NearSolPolity;

internal static class DurationArg
{
  public static bool TryParse(string? text, out long hours)
  {
    hours = 0;
    if (string.IsNullOrWhiteSpace(text))
    {
      return false;
    }

    text = text.Trim();
    var suffix = text[^1];
    var numberPart = char.IsLetter(suffix) ? text[..^1] : text;
    if (!decimal.TryParse(numberPart, System.Globalization.NumberStyles.Number,
          System.Globalization.CultureInfo.InvariantCulture, out var value)
        || value <= 0m)
    {
      return false;
    }

    hours = suffix switch
    {
      'd' or 'D' => (long)Math.Ceiling(value * SimulationHour.HoursPerDay),
      'h' or 'H' => (long)Math.Ceiling(value),
      _ when !char.IsLetter(suffix) => (long)Math.Ceiling(value),
      _ => 0,
    };
    return hours > 0;
  }

  public static string Format(long hours) =>
    hours % SimulationHour.HoursPerDay == 0
      ? $"{hours / SimulationHour.HoursPerDay}d ({hours}h)"
      : $"{hours}h (~{hours / (double)SimulationHour.HoursPerDay:0.#}d)";
}

internal static class HeadlessReport
{
  public static void Write(
    EconomySimulation sim,
    PolityWorld.Ids ids,
    CreditCirculation credits,
    decimal openingLiquid,
    long requestedHours,
    TimeSpan wall,
    NearSolAgents.Bundle agents)
  {
    var world = sim.State.World;
    var hh = world.Cohorts.Sum(c => c.BudgetRemaining.Amount);
    var delivered = world.TransportStats.CargoDelivered.Value;
    var day = sim.State.Clock.Date.DayIndex;
    var liquidDelta = credits.LiquidStock - openingLiquid;
    var openBuy = world.HubOrders.Count(o => !o.IsFilled && o.Side == HubOrderSide.Buy);
    var openSell = world.HubOrders.Count(o => !o.IsFilled && o.Side == HubOrderSide.Sell);

    var lines = new List<string>
    {
      "=== Near-Sol Tycoon report ===",
      $"Sim time:     day {day} ({sim.State.Clock.HourIndex}h)  requested {DurationArg.Format(requestedHours)}",
      $"Wall clock:   {wall.TotalSeconds:0.#}s",
      $"Hash:         {sim.State.Hash:X16}",
      "",
      "— Money stock —",
      $"Liquid:       {credits.LiquidStock:0}  (open {openingLiquid:0}, Δ {liquidDelta:0})",
      $"  vs trade:   Δ − exports + imports = {liquidDelta - credits.ExportRevenue + credits.ImportSpend:0} (want ~0)",
      $"  Households: {hh:0}",
      $"Inv book $:   {credits.InventoryBookValue:0}",
      $"Wages→hh:     {credits.WagesDistributed:0}",
      $"Imports:      {credits.ImportSpend:0}",
      $"Exports:      {credits.ExportRevenue:0}  (Raw qty {credits.ExportQty:0} · fills {credits.ExportFills})",
      $"Tolls→Civics: {credits.TollsToTreasury:0.##}",
      $"Ventures:     {agents.Ventures.Count} HH tramp hulls launched",
      "",
      "— Finance —",
      $"Loans:        active {credits.ActiveLoans}  originated {credits.LoansOriginated}  defaults {credits.LoansDefaulted}",
      $"Principal:    {credits.PrincipalOutstanding:0}",
      $"Interest:     accrued {credits.InterestAccrued:0.##}  repaid-cash {credits.InterestPaid:0.##}",
      "",
      "— Entities (kind / registry / frozen) —",
    };

    foreach (var (name, firmId) in ids.Firms)
    {
      var entity = world.Entities.GetValueOrDefault(firmId);
      var kind = entity?.Kind.ToString() ?? "?";
      var reg = entity?.RegistryId ?? "—";
      var frozen = entity?.CreditFrozen == true ? "frozen" : "ok";
      lines.Add($"  {name,-9} {kind,-5}  registry {reg}  credit {frozen}");
    }

    lines.Add("");
    lines.Add("— Ownership —");
    if (world.OwnershipClaims.Count == 0)
    {
      lines.Add("  (none)");
    }
    else
    {
      foreach (var claim in world.OwnershipClaims
                 .OrderBy(c => c.IssuerFirmId.Value)
                 .ThenBy(c => c.OwnerFirmId.Value))
      {
        var issuer = FirmName(ids, claim.IssuerFirmId);
        var owner = FirmName(ids, claim.OwnerFirmId);
        lines.Add($"  {owner} owns {claim.Fraction:0.##} of {issuer}");
      }
    }

    lines.Add("");
    lines.Add("— Firms (cash / rev / COGS / wages / interest / notes) —");

    foreach (var (name, firmId) in ids.Firms)
    {
      var ledger = world.Ledgers[firmId];
      lines.Add(
        $"  {name,-9} cash {ledger.Cash.Amount,7:0}  rev {Abs(ledger, AccountRole.Revenue),7:0}  " +
        $"cogs {Abs(ledger, AccountRole.CostOfGoodsSold),6:0}  wage {Abs(ledger, AccountRole.WageExpense),6:0}  " +
        $"intE {Abs(ledger, AccountRole.InterestExpense),5:0}  intI {Abs(ledger, AccountRole.InterestIncome),5:0}  " +
        $"NP {Abs(ledger, AccountRole.NotesPayable),5:0}  NR {Abs(ledger, AccountRole.NotesReceivable),5:0}");
    }

    lines.Add("");
    lines.Add("— Macro events (counts) —");
    lines.Add(
      $"Credit:      originated {credits.LoansOriginated}  defaults {credits.LoansDefaulted}  " +
      $"freezes {credits.CreditFreezes} (now frozen {credits.CreditFrozenFirms})");
    lines.Add(
      $"Ownership:   changes {credits.OwnershipChanges}  dividends {credits.Dividends} " +
      $"(cash {credits.DividendCash:0})  absorbs {credits.FacilitiesAbsorbed}");
    lines.Add(
      $"Capacity:    upgrades {credits.FacilityUpgrades}  upgrade-fails {credits.FacilityUpgradeFails}");
    lines.Add(
      $"B2B xfer:    fills {credits.B2bFills} (qty {credits.B2bQty:0})  " +
      $"fail cash {credits.B2bFailCash} / stock {credits.B2bFailStock}");

    lines.Add("");
    lines.Add("— Summary ratios —");
    var firmCash = ids.Firms.Sum(f => world.Ledgers[f.Id].Cash.Amount);
    var totalCashPool = firmCash + hh;
    var days = Math.Max(1, day);
    lines.Add(
      $"Cash split:  firms {firmCash:0} ({Pct(firmCash, totalCashPool)})  " +
      $"hh {hh:0} ({Pct(hh, totalCashPool)})  inv-book {credits.InventoryBookValue:0}");
    lines.Add($"Per day:     fills {credits.BookFills / (decimal)days:0.##}  " +
              $"delivered {delivered / days:0.##}  produced {credits.Produced / days:0.##}  " +
              $"retail {credits.RetailSold / days:0.##}");
    lines.Add(
      $"Throughput:  retail/produced {Ratio(credits.RetailSold, credits.Produced)}  " +
      $"delivered/departed {Ratio(delivered, credits.Departed)}  " +
      $"fill qty/fill {Ratio(credits.BookFillQty, credits.BookFills)}");
    lines.Add(
      $"Sink health: retail/hh-budget {Ratio(credits.RetailSold * PolityWorld.GoodsSell, hh)}  " +
      $"(shelf $ {PolityWorld.GoodsSell:0})  wages→retail {Ratio(credits.RetailSold * PolityWorld.GoodsSell, credits.WagesDistributed)}");
    lines.Add("Firm cash % of firm pool:");
    if (firmCash > 0m)
    {
      foreach (var (name, firmId) in ids.Firms)
      {
        var c = world.Ledgers[firmId].Cash.Amount;
        lines.Add($"  {name,-9} {c,7:0}  {Pct(c, firmCash)}");
      }
    }

    var skuNow = credits.InventoryBySku();
    lines.Add("");
    lines.Add("— Ops inventory (SKU qty) —");
    lines.Add(
      $"  Raw {skuNow.Raw:0}  Capital {skuNow.Capital:0}  Final {skuNow.Final:0}  Energy {skuNow.Energy:0}");
    lines.Add(
      $"  Final is the household consumption sink; Energy fuels tramp legs.");

    var coreSnap = world.CoreState.Snapshot();
    lines.Add("");
    lines.Add("— Core authority (period settle) —");
    lines.Add(
      $"  Period {coreSnap.Period}  entities {coreSnap.EntityCount}  regions {coreSnap.RegionCount}  " +
      $"cohorts {coreSnap.CohortCount}");
    lines.Add(
      $"  Cash {coreSnap.TotalCash.Amount:0}  deposits {coreSnap.TotalDeposits.Amount:0}  " +
      $"broad {coreSnap.BroadMoney.Amount:0}  net-mint/period {coreSnap.NetMoneyCreatedThisPeriod.Amount:0}");
    lines.Add(
      $"  Holdings slots {coreSnap.HoldingSlots}  qty {world.CoreState.Holdings.Values.Sum(h => h.Quantity):0}  " +
      $"in-flight xfers {coreSnap.InFlightTransfers}  " +
      $"loans perf/delinq/def {coreSnap.PerformingLoans}/{coreSnap.DelinquentLoans}/{coreSnap.DefaultedLoans}");
    lines.Add(
      "  Note: ops FirmLedger + cohort budgets still drive NearSol agents; Core accrues " +
      "via delivery credits + daily Advance (dual books until Phase 2 drain completes).");

    if (credits.Milestones.Count > 0)
    {
      lines.Add("");
      lines.Add("— Milestones (cumulative) —");
      lines.Add(
        "  day   liquid  firm$    hh   Final  Raw  Cap  Eng  retail  fills  deliv  coreP  coreHold");
      MacroSnapshot? prev = null;
      foreach (var m in credits.Milestones)
      {
        lines.Add(
          $"  {m.DayIndex,4}  {m.Liquid,7:0}  {m.FirmCash,6:0}  {m.Households,5:0}  " +
          $"{m.SkuFinal,5:0}  {m.SkuRaw,4:0}  {m.SkuCapital,4:0}  {m.SkuEnergy,4:0}  " +
          $"{m.RetailSold,6:0}  {m.BookFills,5}  {m.Delivered,5:0}  {m.CorePeriod,5}  {m.CoreHoldingQty,7:0}");
        if (prev is { } p)
        {
          var dDays = Math.Max(1, m.DayIndex - p.DayIndex);
          lines.Add(
            $"       Δ/day retail {(m.RetailSold - p.RetailSold) / dDays:0.##}  " +
            $"fills {(m.BookFills - p.BookFills) / (decimal)dDays:0.##}  " +
            $"deliv {(m.Delivered - p.Delivered) / dDays:0.##}  " +
            $"hh Δ {m.Households - p.Households:0}  firm$ Δ {m.FirmCash - p.FirmCash:0}  " +
            $"Final Δ {m.SkuFinal - p.SkuFinal:0}  coreHold Δ {m.CoreHoldingQty - p.CoreHoldingQty:0}");
        }

        prev = m;
      }
    }

    if (credits.MacroLog.Count > 0)
    {
      lines.Add("");
      lines.Add("— Macro event log (recent) —");
      foreach (var line in credits.MacroLog)
      {
        lines.Add($"  {line}");
      }
    }

    lines.Add("");
    lines.Add("— Habitats / regions —");
    foreach (var region in world.Regions.Values.OrderBy(r => r.AreaId.Value))
    {
      var usedLive = world.UsedLivingHouseholds(region.AreaId);
      var usedProd = world.UsedProductionSlots(region.AreaId);
      var pool = world.Cohorts
        .Where(c => c.Definition.Area.Equals(region.AreaId))
        .Sum(c => HouseholdMath.LaborHoursPerTick(
          c.Definition.Population,
          c.Definition.Productivity));
      lines.Add(
        $"  {region.AreaId.Value.ToString("N")[..8]}…  living {usedLive}/{region.LivingCapacityHouseholds}  " +
        $"mfg-slots {usedProd}/{region.ProductionSlots}  pool-h/tick {pool:0.##}");
    }

    lines.Add("");
    lines.Add("— Household cohorts (budget / comfort / productivity) —");
    var comfortHolds = agents.Households.Count(a => a.LastDecision == "comfort hold");
    lines.Add($"  Agents: {agents.Households.Count}  comfort holds (last pulse): {comfortHolds}");
    foreach (var cohort in world.Cohorts.OrderByDescending(c => c.BudgetRemaining.Amount).Take(8))
    {
      var floor = world.ComfortFloor(cohort).Amount;
      var above = world.IsAboveComfort(cohort) ? "above" : "hold";
      var prod = cohort.Definition.Productivity.ToString();
      lines.Add(
        $"  bud {cohort.BudgetRemaining.Amount,7:0}  floor {floor,5:0}  {above,-5}  {prod,-7}  " +
        $"hh {cohort.Definition.Population.Value}");
    }

    lines.Add("");
    lines.Add("— Facility owners —");
    foreach (var fac in world.Facilities.Values.OrderBy(f => f.Id.Value).Take(8))
    {
      lines.Add($"  {fac.Id.Value.ToString("N")[..8]}… → {FirmName(ids, fac.FirmId)}");
    }

    var depth = world.HubOrders.Where(o => !o.IsFilled)
      .GroupBy(o => PolityWorld.SkuLabel(o.ProductId, ids))
      .OrderByDescending(g => g.Count())
      .Take(4)
      .Select(g =>
      {
        var buy = g.Where(o => o.Side == HubOrderSide.Buy).Sum(o => o.Remaining.Value);
        var sell = g.Where(o => o.Side == HubOrderSide.Sell).Sum(o => o.Remaining.Value);
        return $"{g.Key} b{buy:0}/s{sell:0}";
      });

    lines.AddRange(
    [
      "",
      "— Activity —",
      $"Produced:     {credits.Produced:0}",
      $"Retail sold:  {credits.RetailSold:0}",
      $"Book fills:   {credits.BookFills}  (qty {credits.BookFillQty:0})  open buy {openBuy} / sell {openSell}",
      $"Book depth:   {string.Join(" · ", depth)}",
      $"Delivered:    {delivered:0}",
      $"Departed:     {credits.Departed}",
      $"Fuel burned:  {world.TransportStats.FuelBurned.Value:0.#}",
      $"Plan fails:   {world.TransportStats.FailedPlans}",
      "",
      "— Agents —",
      $"Mining:       {agents.Mining.LastDecision}",
      $"Industry:     {agents.Industry.LastDecision}",
      $"Station:      {agents.Station.LastDecision}",
    ]);
    for (var i = 0; i < agents.Carriers.Count; i++)
    {
      var c = agents.Carriers[i];
      var label = i == 0 ? "Carrier" : $"Tramp{i + 1}";
      lines.Add($"{label + ":",-14}{c.LastDecision} | {c.LastEval}");
    }

    lines.AddRange(
    [
      $"Treasury:     {agents.Treasury.LastDecision}",
      $"Sol export:   {agents.SolExport.LastDecision}",
      $"Ventures:     {agents.VenturesAgent.LastDecision}  started {agents.VenturesAgent.VenturesStarted}",
      "",
      "— Travel —",
      $"Cruise:       {AstroEconomyBridge.CruiseDaysPerLy:0.##} d/ly",
      $"Fleet:        {agents.Carriers.Count} tramps (seed {PolityWorld.TrampFleetSize} + ventures)  MinMargin {PolityWorld.MinMargin:0.##}",
      $"Roles:        {ids.RoleSummary}",
      "Agents:       Novolis.Economy.Agents heuristics + DeterministicRandom",
      "Civics:       Station entity (tolls / treasury / ownership) — product copy",
      "Geography:    SystemProfile potentials (Astro.Assessment) gate settlement/mining",
      $"Chaos:        Sol Raw export @ {PolityWorld.OreExport:0} · HH tramp ventures (borrow vs hull)",
    ]);

    if (agents.Ventures.Count > 0)
    {
      lines.Add("— HH tramp ventures —");
      foreach (var (tramp, ownerHh, name) in agents.Ventures)
      {
        var cash = world.Ledgers.TryGetValue(tramp, out var led) ? led.Cash.Amount : 0m;
        lines.Add($"  {name}  cash {cash:0}  owner {ownerHh.Value.ToString("N")[..8]}…");
      }
    }

    var failReasons = credits.PlanFailReasons
      .OrderByDescending(kv => kv.Value)
      .Take(5)
      .Select(kv => $"{kv.Key}×{kv.Value}");
    if (failReasons.Any())
    {
      lines.Add($"Fail reasons: {string.Join(", ", failReasons)}");
    }

    lines.Add("===============================");

    foreach (var line in lines)
    {
      AnsiConsole.WriteLine(line);
    }
  }

  private static string FirmName(PolityWorld.Ids ids, FirmId id)
  {
    foreach (var (name, firmId) in ids.Firms)
    {
      if (firmId.Equals(id))
      {
        return name;
      }
    }

    return id.Value.ToString("N")[..8];
  }

  private static decimal Abs(FirmLedger ledger, AccountRole role) =>
    Math.Abs(ledger.Balance(role).Amount);

  private static string Pct(decimal part, decimal whole) =>
    whole <= 0m ? "—" : $"{100m * part / whole:0.#}%";

  private static string Ratio(decimal num, decimal den) =>
    den <= 0m ? "—" : $"{num / den:0.##}";
}
