using ScarcityRationingLab;
using Novolis.Economy;
using Novolis.Economy.Simulation;
using Novolis.Economy.Simulation.Models;

Console.WriteLine("Fixed-price scarcity and quantity rationing");
Console.WriteLine("Posted price: $10; affordable demand: 100 units");
var modelRun = await SimulationRunner.RunAsync(
    new SimulationRunRequest(
        SimulationModels.DeterministicBounded(),
        Seed: 7,
        Duration: SimulationDuration.OneDay));
Console.WriteLine(
    $"Selected model: {modelRun.Manifest.ModelId} v{modelRun.Manifest.ModelVersion}; " +
    $"bounded output: {modelRun.Metrics.Get("physical-production"):0.####}");
Console.WriteLine();

Print("abundant supply", ScarcityScenario.Run(100m));
Print("scarce supply", ScarcityScenario.Run(3m));
Console.WriteLine();
Console.WriteLine(
    "Lesson: at the same posted price, lower supply reduces quantity sold " +
    "and creates unmet demand. This lab does not discover an equilibrium price.");

static void Print(string label, ScarcityResult result)
{
    Console.WriteLine(label);
    Console.WriteLine($"  available supply: {result.AvailableSupply:0.####}");
    Console.WriteLine($"  affordable demand: {result.AffordableDemand:0.####}");
    Console.WriteLine($"  units sold: {result.UnitsSold:0.####}");
    Console.WriteLine($"  unmet demand: {result.UnmetDemand:0.####}");
    Console.WriteLine(
        $"  trade: buyer purchased {result.UnitsSold:0.####} units " +
        $"from seller at ${ScarcityScenario.PostedPrice:0.##}");
    Console.WriteLine(
        $"  goods flow: seller -> buyer {result.UnitsSold:0.####}; " +
        $"seller still has {result.SellerInventory:0.####}");
    Console.WriteLine($"  buyer spending: ${result.BuyerSpending.Amount:0.##}");
    Console.WriteLine($"  seller revenue: ${result.SellerRevenue.Amount:0.##}");
    Console.WriteLine(
        $"  unmet demand value at posted price: ${result.UnmetDemandValue.Amount:0.##}");
    Console.WriteLine($"  buyer cash: ${result.BuyerCash.Amount:0.##}");
    Console.WriteLine($"  seller cash: ${result.SellerCash.Amount:0.##}");
    Console.WriteLine($"  cash conserved: {result.TotalCash.Amount:0.##} / $1,000");
    Console.WriteLine($"  goods conserved: {result.TotalGoods:0.####}");
    Console.WriteLine($"  snapshot: {result.Snapshot}");
}
