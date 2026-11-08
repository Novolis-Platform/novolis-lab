using CalypsoCad.Model;

namespace CalypsoCad.Generation;

internal sealed record GenerationResult(
    IReadOnlyList<CadPart> Parts,
    IReadOnlyList<StructuralConnection> Connections,
    IReadOnlyList<GeneratedRepresentation> Representations,
    IReadOnlyList<string> Diagnostics)
{
    public static GenerationResult Empty { get; } = new([], [], [], []);

    public bool HasFailures =>
        Diagnostics.Any(d => d.StartsWith("FAIL", StringComparison.OrdinalIgnoreCase));
}
