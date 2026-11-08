using CalypsoCad.Hierarchy;
using CalypsoCad.Publication;
using CalypsoCad.Structure;
using Novolis.Cad.Generation;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation;

/// <summary>
/// <see cref="ICadModelGenerator"/> wrapper around the existing Calypso lock/pipeline.
/// Whole-document replace; no Avalonia/camera dependencies.
/// </summary>
internal sealed class CalypsoModelGenerator : ICadModelGenerator
{
    public const string GeneratorId = "calypso.lock";

    public string Id => GeneratorId;

    public string DisplayName => "Calypso tramp freighter";

    public ValueTask<CadGenerationResult> GenerateAsync(
        CadGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var structuralBay = request.Options is not null
            && request.Options.TryGetValue("mode", out var mode)
            && string.Equals(mode, "structural-bay", StringComparison.OrdinalIgnoreCase);

        CadDocumentAdapter.Reset();
        string dir;
        string prefix;
        if (structuralBay)
        {
            dir = CalypsoLockGenerator.GenerateStructuralBay();
            prefix = "calypso-structural-bay";
        }
        else
        {
            var outDir = request.Options is not null
                && request.Options.TryGetValue("outputDirectory", out var o)
                && !string.IsNullOrWhiteSpace(o)
                ? o
                : null;
            dir = CalypsoLockGenerator.Generate(outDir);
            prefix = "calypso";
        }

        var (layers, shapes, document) = CadDocumentBundle.Read(dir, prefix);
        _ = layers;
        _ = shapes;

        CadDocumentAdapter.LoadFromDocument(document);
        var hierarchy = ShipHierarchyMapper.ToHierarchy(CadDocumentAdapter.Active);

        return ValueTask.FromResult(new CadGenerationResult
        {
            Document = document,
            Hierarchy = hierarchy,
            Ok = true,
            OutputDirectory = dir,
            Diagnostics =
            [
                $"Generated {document.Entities.Count} entities → {dir}",
            ],
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["generator"] = Id,
                ["prefix"] = prefix,
                ["registryParts"] = CadDocumentAdapter.Active.Parts.Count.ToString(),
            },
        });
    }
}
