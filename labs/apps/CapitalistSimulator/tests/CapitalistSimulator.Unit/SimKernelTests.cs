using CapitalistSimulator.Cli;
using CapitalistSimulator.Persistence;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Unit;

public class SimKernelTests
{
    [Test]
    public async Task Catalog_Loads_ProductsAndRecipes()
    {
        var catalog = GameCatalog.LoadEmbedded();
        await Assert.That(catalog.Products.Count).IsGreaterThanOrEqualTo(40);
        await Assert.That(catalog.RecipesByOutput.ContainsKey("wine")).IsTrue();
        await Assert.That(catalog.FirmTypes.ContainsKey("retail_super")).IsTrue();
    }

    [Test]
    public async Task LinkRules_AutoLink_RetailChain()
    {
        var firm = new Firm { Kind = FirmKind.Retail, LayoutW = 4, LayoutH = 3 };
        firm.Units.Add(new FunctionalUnit { Kind = UnitKind.Purchasing, X = 0, Y = 0 });
        firm.Units.Add(new FunctionalUnit { Kind = UnitKind.Sales, X = 1, Y = 0 });
        firm.Units.Add(new FunctionalUnit { Kind = UnitKind.Inventory, X = 2, Y = 0 });
        LinkRules.AutoLink(firm);
        await Assert.That(firm.Links.Count).IsGreaterThan(0);
        await Assert.That(LinkRules.CanLink(UnitKind.Purchasing, UnitKind.Sales)).IsTrue();
        await Assert.That(LinkRules.CanLink(UnitKind.Sales, UnitKind.Purchasing)).IsFalse();
    }

    [Test]
    public async Task Manufacture_ConsumesInputs_ProducesOutput()
    {
        var world = WorldFactory.Create(ScenarioId.Sandbox, startingCash: 5_000_000, aiCount: 0, seed: 7);
        var proc = new CommandProcessor(world);
        var city = world.Cities[0].Name;
        proc.Apply(new BuildFirmCommand(city, "factory_s", 1, 1, "Steelworks"));
        var firm = world.FirmsOf(world.Player.Id).First(f => f.Kind == FirmKind.Factory);
        world.GetOrCreateLot(firm, "iron", 0.6).Quantity = 100;
        world.GetOrCreateLot(firm, "iron", 0.6).UnitCost = 8;
        world.FindLot(firm, "iron")!.UnitCost = 8;
        world.GetOrCreateLot(firm, "coal", 0.5).Quantity = 100;
        world.FindLot(firm, "coal")!.UnitCost = 6;
        var mfg = firm.Units.First(u => u.Kind == UnitKind.Manufacturing);
        proc.Apply(new ConfigureManufacturingCommand(firm.Id, mfg.Id, "steel", 20));
        new MonthTick(world).RunMonth();
        var steel = world.FindLot(firm, "steel");
        await Assert.That(steel).IsNotNull();
        await Assert.That(steel!.Quantity).IsGreaterThan(0);
    }

    [Test]
    public async Task Retail_ClearsDemand_GeneratesRevenue()
    {
        var world = WorldFactory.Create(ScenarioId.Sandbox, startingCash: 5_000_000, aiCount: 0, seed: 11);
        HeadlessRunner.BootstrapRetail(world);
        var before = world.Player.Cash;
        new MonthTick(world).RunMonth();
        var sold = world.LastMonthSales.Where(s => s.CorpId.Equals(world.Player.Id)).Sum(s => s.UnitsSold);
        await Assert.That(sold).IsGreaterThan(0);
        await Assert.That(world.Player.MonthRevenue).IsGreaterThan(0);
        await Assert.That(world.Player.Cash).IsNotEqualTo(before);
    }

    [Test]
    public async Task StarterRetail_IsProfitable_InFirstMonth()
    {
        var world = WorldFactory.Create(ScenarioId.Sandbox, startingCash: 2_000_000, aiCount: 0, seed: 42);
        StarterBootstrap.EnsureStarterRetail(world);
        new MonthTick(world).RunMonth();
        var pnl = world.Player.MonthRevenue - world.Player.MonthExpense;
        await Assert.That(world.Player.MonthRevenue).IsGreaterThan(5_000);
        await Assert.That(pnl).IsGreaterThan(0);
    }

    [Test]
    public async Task Coach_PointsAtAdvance_AfterStarter()
    {
        var world = WorldFactory.Create(aiCount: 0, seed: 1);
        StarterBootstrap.EnsureStarterRetail(world);
        var step = TutorialCoach.Next(world);
        await Assert.That(step.PrimaryAction).IsEqualTo("Advance month");
    }

    [Test]
    public async Task Bank_BorrowAndRepay()
    {
        var world = WorldFactory.Create(aiCount: 0, seed: 3);
        var proc = new CommandProcessor(world);
        var cash0 = world.Player.Cash;
        await Assert.That(proc.Apply(new BorrowCommand(100_000)).Ok).IsTrue();
        await Assert.That(world.Player.Cash).IsEqualTo(cash0 + 100_000);
        await Assert.That(proc.Apply(new RepayCommand(40_000)).Ok).IsTrue();
        await Assert.That(world.Player.Loans.Sum(l => l.Principal)).IsEqualTo(60_000);
    }

    [Test]
    public async Task Stock_BuyShares()
    {
        var world = WorldFactory.Create(aiCount: 1, seed: 5);
        var ai = world.Corporations.First(c => c.IsAi);
        var proc = new CommandProcessor(world);
        var r = proc.Apply(new BuySharesCommand(ai.Id, 1000));
        await Assert.That(r.Ok).IsTrue();
        var held = world.Holdings.First(h => h.Owner.Equals(world.Player.Id) && h.Issuer.Equals(ai.Id));
        await Assert.That(held.Shares).IsEqualTo(1000);
    }

    [Test]
    public async Task SaveLoad_RoundTrip()
    {
        var world = WorldFactory.Create(ScenarioId.RetailProfit, aiCount: 1, seed: 9);
        HeadlessRunner.BootstrapRetail(world);
        new MonthTick(world).RunMonth();
        var dir = Path.Combine(Path.GetTempPath(), "cap-sim-test-" + Guid.NewGuid().ToString("N"));
        var store = new SaveStore(dir);
        store.Save(world, "t1");
        var loaded = store.Load("t1");
        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.Day).IsEqualTo(world.Day);
        await Assert.That(loaded.Player.Cash).IsEqualTo(world.Player.Cash);
        await Assert.That(loaded.Firms.Count).IsEqualTo(world.Firms.Count);
        Directory.Delete(dir, recursive: true);
    }

    [Test]
    public async Task WinCondition_RetailProfit_CanTrigger()
    {
        var world = WorldFactory.Create(ScenarioId.RetailProfit, aiCount: 0, seed: 2);
        world.Player.TrailingYearProfit = world.ScenarioTargetProfit;
        // need 2 retail firms
        var proc = new CommandProcessor(world);
        var city = world.Cities[0].Name;
        proc.Apply(new BuildFirmCommand(city, "retail_conven", 1, 1));
        proc.Apply(new BuildFirmCommand(city, "retail_conven", 1, 4));
        WinConditions.Evaluate(world);
        await Assert.That(world.Win.Won).IsTrue();
    }
}
