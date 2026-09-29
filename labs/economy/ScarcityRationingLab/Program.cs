using ScarcityRationingLab;
using Novolis.Economy.Models.DeterministicBounded;
using Novolis.Economy.Simulation;

Console.WriteLine("Fixed-price scarcity and quantity rationing");
Console.WriteLine("Posted price: $10; affordable demand: 100 units");
var modelRun = EconomicModelRunner.Run(
    new EconomicModelRunRequest(
        new DeterministicBoundedModel(),
        DeterministicBoundedScenario.Baseline,
        Seed: 7,
        Ticks: 24));
Console.WriteLine(
    $"Selected model: {modelRun.Manifest.Model.Id} v{modelRun.Manifest.Model.Version}; " +
    $"bounded output: {modelRun.Observations
        .Where(observation => observation.Name == "food-produced")
        .Sum(observation => observation.Value):0.####}");
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
