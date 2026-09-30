namespace CapitalistSimulator.Sim;

internal sealed class FunctionalUnit
{
    public UnitId Id { get; init; } = UnitId.New();
    public UnitKind Kind { get; init; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Level { get; set; } = 1;
    public double Training { get; set; } = 0.3;
    public bool PrivateLabel { get; set; }

    // Purchasing
    public string? PurchaseProductId { get; set; }
    public decimal PurchaseQtyTarget { get; set; }
    public bool PurchaseFromSeaport { get; set; } = true;
    public FirmId? PurchaseFromFirm { get; set; }

    // Manufacturing
    public string? RecipeOutputId { get; set; }
    public decimal ProductionRate { get; set; } = 10;

    // Sales / retail slot
    public string? SalesProductId { get; set; }
    public decimal SalesPrice { get; set; }
    public decimal LastSold { get; set; }
    public decimal LastUnmetDemand { get; set; }

    // Advertising
    public string? AdProductId { get; set; }
    public string? AdClass { get; set; }
    public decimal AdBudget { get; set; }

    // Extract
    public ExtractKind ExtractKind { get; set; }
    public string? ExtractProductId { get; set; }
    public decimal ExtractYield { get; set; } = 40;

    // R&D
    public string? RdTargetProductId { get; set; }
    public int RdMonthsRemaining { get; set; }
    public double RdProgress { get; set; }
}
