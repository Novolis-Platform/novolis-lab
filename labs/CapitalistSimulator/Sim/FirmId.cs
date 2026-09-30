namespace CapitalistSimulator.Sim;

internal readonly record struct FirmId(Guid Value)
{
    public static FirmId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N")[..8];
}
