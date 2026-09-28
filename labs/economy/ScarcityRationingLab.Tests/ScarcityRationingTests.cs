using ScarcityRationingLab;

namespace ScarcityRationingLab.Tests;

public sealed class ScarcityRationingTests
{
    [Test]
    public async Task Abundant_supply_sells_all_affordable_demand()
    {
        var result = ScarcityScenario.Run(100m);

        await Assert.That(result.UnitsSold).IsEqualTo(100m);
        await Assert.That(result.UnmetDemand).IsEqualTo(0m);
        await Assert.That(result.TradeValue.Amount).IsEqualTo(1_000m);
        await Assert.That(result.BuyerSpending.Amount).IsEqualTo(1_000m);
        await Assert.That(result.SellerRevenue.Amount).IsEqualTo(1_000m);
        await Assert.That(result.BuyerInventory).IsEqualTo(100m);
        await Assert.That(result.SellerInventory).IsEqualTo(0m);
    }

    [Test]
    public async Task Scarce_supply_rations_quantity_at_the_same_price()
    {
        var result = ScarcityScenario.Run(3m);

        await Assert.That(result.UnitsSold).IsEqualTo(3m);
        await Assert.That(result.UnmetDemand).IsEqualTo(97m);
        await Assert.That(result.TradeValue.Amount).IsEqualTo(30m);
        await Assert.That(result.BuyerSpending.Amount).IsEqualTo(30m);
        await Assert.That(result.SellerRevenue.Amount).IsEqualTo(30m);
        await Assert.That(result.UnmetDemandValue.Amount).IsEqualTo(970m);
        await Assert.That(result.BuyerInventory).IsEqualTo(3m);
        await Assert.That(result.SellerInventory).IsEqualTo(0m);
    }

    [Test]
    public async Task Cash_and_goods_are_conserved()
    {
        var result = ScarcityScenario.Run(3m);

        await Assert.That(result.BuyerCash.Amount + result.SellerCash.Amount)
            .IsEqualTo(1_000m);
        await Assert.That(result.TotalCash.Amount).IsEqualTo(1_000m);
        await Assert.That(result.TotalGoods).IsEqualTo(3m);
    }

    [Test]
    public async Task Inventory_is_non_negative_and_core_invariants_hold()
    {
        var result = ScarcityScenario.Run(3m);

        await Assert.That(result.BuyerInventory).IsGreaterThanOrEqualTo(0m);
        await Assert.That(result.SellerInventory).IsGreaterThanOrEqualTo(0m);
        await Assert.That(result.Snapshot).IsNotNull();
    }

    [Test]
    public async Task Identical_runs_have_identical_snapshots()
    {
        var first = ScarcityScenario.Run(3m);
        var second = ScarcityScenario.Run(3m);

        await Assert.That(first.Snapshot).IsEqualTo(second.Snapshot);
    }
}
