namespace CapitalistSimulator.Sim;

internal readonly record struct CorpId(Guid Value)
{
    public static CorpId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N")[..8];
}

internal readonly record struct CityId(Guid Value)
{
    public static CityId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N")[..8];
}

internal readonly record struct FirmId(Guid Value)
{
    public static FirmId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N")[..8];
}

internal readonly record struct UnitId(Guid Value)
{
    public static UnitId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N")[..8];
}

internal readonly record struct ProductId(string Value)
{
    public override string ToString() => Value;
}
