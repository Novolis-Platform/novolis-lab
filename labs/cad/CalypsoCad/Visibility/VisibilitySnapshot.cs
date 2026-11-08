namespace CalypsoCad.Visibility;

/// <summary>Serializable set of explicit local overrides (Inherit is absence).</summary>
internal sealed class VisibilitySnapshot
{
    public Dictionary<string, LocalVisibility> Overrides { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Label { get; init; }

    public VisibilitySnapshot Clone() =>
        new()
        {
            Label = Label,
            Overrides = new Dictionary<string, LocalVisibility>(Overrides, StringComparer.OrdinalIgnoreCase),
        };
}
