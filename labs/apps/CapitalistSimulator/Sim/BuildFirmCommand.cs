namespace CapitalistSimulator.Sim;

internal sealed record BuildFirmCommand(
    string CityName,
    string FirmTypeId,
    int TileX,
    int TileY,
    string? Name = null) : PlayerCommand;
