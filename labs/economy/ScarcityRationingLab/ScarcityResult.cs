using System.Globalization;
using Novolis.Economy.Core;
using Novolis.Economy.Core.Finance;
using Novolis.Economy.Core.Holdings;
using Novolis.Economy.Core.Invariants;
using Novolis.Economy.Core.Transactions;
using Novolis.Economy.Primitives;
using Novolis.Economy.Simulation.Bounded;

namespace ScarcityRationingLab;

/// <summary>One fixed-price quantity-rationing experiment.</summary>
public sealed record ScarcityResult(
    decimal AvailableSupply,
    decimal AffordableDemand,
    decimal UnitsSold,
    decimal UnmetDemand,
    Money AffordableDemandValue,
    Money TradeValue,
    Money BuyerSpending,
    Money SellerRevenue,
    Money UnmetDemandValue,
    Money BuyerCash,
    Money SellerCash,
    decimal BuyerInventory,
    decimal SellerInventory,
    Money TotalCash,
    decimal TotalGoods,
    string Snapshot);
