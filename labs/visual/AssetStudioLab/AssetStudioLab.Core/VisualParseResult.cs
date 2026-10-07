namespace AssetStudioLab;

/// <summary>Outcome of parsing visual source.</summary>
public sealed record VisualParseResult(
    VisualDocument? Document,
    IReadOnlyList<VisualParseError> Errors)
{
    public bool Success => Document is not null && Errors.Count == 0;

    public static VisualParseResult Ok(VisualDocument document) => new(document, []);

    public static VisualParseResult Fail(string message) =>
        new(null, [new VisualParseError(message)]);
}
