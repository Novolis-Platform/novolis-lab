namespace CapitalistSimulator.Sim;

internal sealed class City
{
    public CityId Id { get; init; } = CityId.New();
    public string Name { get; set; } = "";
    public int Width { get; set; } = 16;
    public int Height { get; set; } = 12;
    public CityTile[,] Tiles { get; set; } = new CityTile[16, 12];
    public double SpendingLevel { get; set; } = 1.0;
    public double SalaryLevel { get; set; } = 1.0;
    public EconomicClimate Climate { get; set; } = EconomicClimate.Stable;
    public decimal Population { get; set; } = 100_000;
}
