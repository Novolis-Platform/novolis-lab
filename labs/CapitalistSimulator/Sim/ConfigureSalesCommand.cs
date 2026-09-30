namespace CapitalistSimulator.Sim;

internal sealed record ConfigureSalesCommand(
    FirmId FirmId,
    UnitId UnitId,
    string ProductId,
    decimal Price) : PlayerCommand;
