namespace CapitalistSimulator.Sim;

internal sealed record ConfigureExtractCommand(
    FirmId FirmId,
    UnitId UnitId,
    ExtractKind Kind,
    string ProductId,
    decimal Yield) : PlayerCommand;
