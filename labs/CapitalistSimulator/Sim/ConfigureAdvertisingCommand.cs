namespace CapitalistSimulator.Sim;

internal sealed record ConfigureAdvertisingCommand(
    FirmId FirmId,
    UnitId UnitId,
    string? ProductId,
    string? ProductClass,
    decimal Budget) : PlayerCommand;
