namespace CapitalistSimulator.Sim;

internal readonly record struct UnitId(Guid Value)
{
    public static UnitId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N")[..8];
}
