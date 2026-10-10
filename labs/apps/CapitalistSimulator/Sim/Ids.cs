namespace CapitalistSimulator.Sim;

internal readonly record struct CorpId(Guid Value)
{
    public static CorpId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N")[..8];
}
