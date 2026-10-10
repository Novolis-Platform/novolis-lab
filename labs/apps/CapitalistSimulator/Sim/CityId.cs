namespace CapitalistSimulator.Sim;

internal readonly record struct CityId(Guid Value)
{
    public static CityId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N")[..8];
}
