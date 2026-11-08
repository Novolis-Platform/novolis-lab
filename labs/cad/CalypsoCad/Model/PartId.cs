namespace CalypsoCad.Model;

/// <summary>Stable identity for a physical part (not a display name).</summary>
internal readonly record struct PartId(string Value)
{
    public override string ToString() => Value;

    public static PartId Of(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new PartId(value);
    }
}
