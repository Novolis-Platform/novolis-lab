namespace CapitalistSimulator.Sim;

internal sealed class CityTile
{
    public TileKind Kind { get; set; } = TileKind.Buildable;
    public FirmId? FirmId { get; set; }
    public decimal LandCost { get; set; } = 5000;
}
