namespace CapitalistSimulator.Sim;

internal sealed record ConfigurePurchasingCommand(
    FirmId FirmId,
    UnitId UnitId,
    string ProductId,
    decimal QtyTarget,
    bool FromSeaport,
    FirmId? FromFirm,
    bool PrivateLabel) : PlayerCommand;
