namespace CapitalistSimulator.Sim;

internal sealed record ConfigureManufacturingCommand(
    FirmId FirmId,
    UnitId UnitId,
    string RecipeOutputId,
    decimal ProductionRate) : PlayerCommand;
