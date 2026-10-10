using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class UnitDto
{
    public Guid Id { get; set; }
    public UnitKind Kind { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Level { get; set; }
    public double Training { get; set; }
    public bool PrivateLabel { get; set; }
    public string? PurchaseProductId { get; set; }
    public decimal PurchaseQtyTarget { get; set; }
    public bool PurchaseFromSeaport { get; set; }
    public Guid? PurchaseFromFirm { get; set; }
    public string? RecipeOutputId { get; set; }
    public decimal ProductionRate { get; set; }
    public string? SalesProductId { get; set; }
    public decimal SalesPrice { get; set; }
    public string? AdProductId { get; set; }
    public string? AdClass { get; set; }
    public decimal AdBudget { get; set; }
    public ExtractKind ExtractKind { get; set; }
    public string? ExtractProductId { get; set; }
    public decimal ExtractYield { get; set; }
    public string? RdTargetProductId { get; set; }
    public int RdMonthsRemaining { get; set; }
    public double RdProgress { get; set; }

    public static UnitDto From(FunctionalUnit u) => new()
    {
        Id = u.Id.Value,
        Kind = u.Kind,
        X = u.X,
        Y = u.Y,
        Level = u.Level,
        Training = u.Training,
        PrivateLabel = u.PrivateLabel,
        PurchaseProductId = u.PurchaseProductId,
        PurchaseQtyTarget = u.PurchaseQtyTarget,
        PurchaseFromSeaport = u.PurchaseFromSeaport,
        PurchaseFromFirm = u.PurchaseFromFirm?.Value,
        RecipeOutputId = u.RecipeOutputId,
        ProductionRate = u.ProductionRate,
        SalesProductId = u.SalesProductId,
        SalesPrice = u.SalesPrice,
        AdProductId = u.AdProductId,
        AdClass = u.AdClass,
        AdBudget = u.AdBudget,
        ExtractKind = u.ExtractKind,
        ExtractProductId = u.ExtractProductId,
        ExtractYield = u.ExtractYield,
        RdTargetProductId = u.RdTargetProductId,
        RdMonthsRemaining = u.RdMonthsRemaining,
        RdProgress = u.RdProgress,
    };

    public FunctionalUnit ToUnit() => new()
    {
        Id = new UnitId(Id),
        Kind = Kind,
        X = X,
        Y = Y,
        Level = Level,
        Training = Training,
        PrivateLabel = PrivateLabel,
        PurchaseProductId = PurchaseProductId,
        PurchaseQtyTarget = PurchaseQtyTarget,
        PurchaseFromSeaport = PurchaseFromSeaport,
        PurchaseFromFirm = PurchaseFromFirm is { } g ? new FirmId(g) : null,
        RecipeOutputId = RecipeOutputId,
        ProductionRate = ProductionRate,
        SalesProductId = SalesProductId,
        SalesPrice = SalesPrice,
        AdProductId = AdProductId,
        AdClass = AdClass,
        AdBudget = AdBudget,
        ExtractKind = ExtractKind,
        ExtractProductId = ExtractProductId,
        ExtractYield = ExtractYield,
        RdTargetProductId = RdTargetProductId,
        RdMonthsRemaining = RdMonthsRemaining,
        RdProgress = RdProgress,
    };
}
