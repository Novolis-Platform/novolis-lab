namespace CapitalistSimulator.Sim;

internal sealed class CorpTech
{
    public Dictionary<string, double> ProductTech { get; } = new(StringComparer.OrdinalIgnoreCase);
}
