namespace CapitalistSimulator.Sim;

internal readonly record struct ProductId(string Value)
{
    public override string ToString() => Value;
}
