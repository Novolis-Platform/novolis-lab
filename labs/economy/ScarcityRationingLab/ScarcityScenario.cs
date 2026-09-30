using System.Globalization;
using Novolis.Economy.Core;
using Novolis.Economy.Core.Finance;
using Novolis.Economy.Core.Holdings;
using Novolis.Economy.Core.Invariants;
using Novolis.Economy.Core.Transactions;
using Novolis.Economy.Primitives;
using Novolis.Economy.Simulation.Bounded;

namespace ScarcityRationingLab;

/// <summary>
/// Builds a minimal Core economy and applies only the posted-price transfer
/// step. It intentionally does not invoke the full simulation pipeline.
/// </summary>
public static class ScarcityScenario
{
    public const decimal PostedPrice = 10m;
    public const decimal BuyerCash = 1_000m;
    public const decimal AffordableDemand = 100m;

    private static readonly LegalEntityId Buyer =
        LegalEntityId.From(Guid.Parse("10000000-0000-4000-8000-000000000001"));
    private static readonly LegalEntityId Seller =
        LegalEntityId.From(Guid.Parse("10000000-0000-4000-8000-000000000002"));
    private static readonly RegionId Region =
        RegionId.From(Guid.Parse("20000000-0000-4000-8000-000000000001"));
    private static readonly CohortId Cohort =
        CohortId.From(Guid.Parse("30000000-0000-4000-8000-000000000001"));
    private static readonly ResourceId Good =
        ResourceId.From(Guid.Parse("40000000-0000-4000-8000-000000000001"));
    private static readonly EconomicAssetId GoodAsset =
        EconomicAssetId.From(Guid.Parse("50000000-0000-4000-8000-000000000001"));
    private static readonly EconomicAssetId MoneyAsset =
        EconomicAssetId.From(Guid.Parse("60000000-0000-4000-8000-000000000001"));

    /// <summary>Runs the experiment with the requested available supply.</summary>
    public static ScarcityResult Run(decimal availableSupply)
    {
        if (availableSupply < 0m)
            throw new ArgumentOutOfRangeException(nameof(availableSupply));

        var before = Seed(availableSupply);
        var after = new TransferOwnershipPaymentsStep().Execute(before);

        var buyerGood = PositionLedger.GetQuantity(
            after,
            EconomicIdentity.For(Buyer),
            Region,
            GoodAsset);
        var sellerGood = PositionLedger.GetQuantity(
            after,
            EconomicIdentity.For(Seller),
            Region,
            GoodAsset);
        var buyerCash = CashLedger.Balance(after, Buyer);
        var sellerCash = CashLedger.Balance(after, Seller);
        var sold = buyerGood;
        var tradeValue = Money.From(sold * PostedPrice);
        var unmetDemand = Math.Max(0m, AffordableDemand - sold);
        var totalCash = CashLedger.Balance(after, Buyer) + CashLedger.Balance(after, Seller);
        var totalGoods = buyerGood + sellerGood;

        InvariantChecker.AssertAll(after);

        return new ScarcityResult(
            availableSupply,
            AffordableDemand,
            sold,
            unmetDemand,
            Money.From(AffordableDemand * PostedPrice),
            tradeValue,
            Money.From(BuyerCash - buyerCash.Amount),
            sellerCash,
            Money.From(unmetDemand * PostedPrice),
            buyerCash,
            sellerCash,
            buyerGood,
            sellerGood,
            totalCash,
            totalGoods,
            Snapshot(
                availableSupply,
                sold,
                buyerCash,
                sellerCash,
                buyerGood,
                sellerGood,
                tradeValue,
                unmetDemand,
                totalCash,
                totalGoods));
    }

    private static EconomyState Seed(decimal availableSupply)
    {
        var state = EconomyState.Empty with
        {
            Entities = new Dictionary<LegalEntityId, LegalEntity>
            {
                [Buyer] = new LegalEntity(Buyer, LegalEntityKind.Household, Money.Zero),
                [Seller] = new LegalEntity(Seller, LegalEntityKind.Firm, Money.Zero)
            },
            Regions = new Dictionary<RegionId, Region>
            {
                [Region] = new Region(Region, 1_000, 1_000m, 1_000m)
            },
            Cohorts = new Dictionary<CohortId, HouseholdCohort>
            {
                [Cohort] = new HouseholdCohort(
                    Cohort,
                    Region,
                    1,
                    new HouseholdProfile(
                        ConsumptionWeight: 1m,
                        SavingsPreference: 0m,
                        LaborQuality: 1m,
                        MigrationPreference: 0m),
                    HouseholdLaborKind.Common,
                    Money.Zero,
                    Buyer)
            },
            Resources = new Dictionary<ResourceId, Resource>
            {
                [Good] = new Resource(
                    Good,
                    "Bread",
                    ResourceKind.ConsumerGood,
                    GoodAsset)
            },
            PostedPrices = new Dictionary<string, PostedPrice>
            {
                [EconomyState.PriceKey(Region, Good)] =
                    new PostedPrice(Region, Good, Money.From(PostedPrice))
            },
            UnitOfAccountAssetId = MoneyAsset,
            Positions = new Dictionary<string, EconomicPosition>(),
            Holdings = new Dictionary<string, ResourceHolding>()
        };

        return EconomicTransactionEngine.Apply(
            state,
            new EconomicTransaction(
                TransactionId.From(
                    Guid.Parse("70000000-0000-4000-8000-000000000001")),
                [
                    new PositionChange(
                        EconomicIdentity.For(Seller),
                        GoodAsset,
                        availableSupply,
                        Region),
                    new PositionChange(
                        EconomicIdentity.For(Buyer),
                        MoneyAsset,
                        BuyerCash,
                        Region: null)
                ],
                "scarcity-lab-seed"));
    }

    private static string Snapshot(
        decimal availableSupply,
        decimal sold,
        Money buyerCash,
        Money sellerCash,
        decimal buyerGood,
        decimal sellerGood,
        Money tradeValue,
        decimal unmetDemand,
        Money totalCash,
        decimal totalGoods) =>
        string.Join(
            "|",
            availableSupply.ToString("0.####", CultureInfo.InvariantCulture),
            sold.ToString("0.####", CultureInfo.InvariantCulture),
            buyerCash.Amount.ToString("0.####", CultureInfo.InvariantCulture),
            sellerCash.Amount.ToString("0.####", CultureInfo.InvariantCulture),
            buyerGood.ToString("0.####", CultureInfo.InvariantCulture),
            sellerGood.ToString("0.####", CultureInfo.InvariantCulture),
            tradeValue.Amount.ToString("0.####", CultureInfo.InvariantCulture),
            unmetDemand.ToString("0.####", CultureInfo.InvariantCulture),
            totalCash.Amount.ToString("0.####", CultureInfo.InvariantCulture),
            totalGoods.ToString("0.####", CultureInfo.InvariantCulture));
}
