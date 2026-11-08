namespace CalypsoCad.Model;

/// <summary>Stable identity for an engineering assembly (frame, bulkhead, deck panel group).</summary>
internal readonly record struct AssemblyId(string Value)
{
    public override string ToString() => Value;

    public static AssemblyId Of(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new AssemblyId(value);
    }
}
