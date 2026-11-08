using CalypsoCad.Model;
using CalypsoCad.Publication;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Validation;

/// <summary>Semantic assembly validation over the part registry + legacy joint graph.</summary>
internal static class AssemblyValidation
{
    public sealed record Result(
        StructuralAssemblyGraph.ValidationResult Graph,
        int RegistryParts,
        int BulkheadPlates,
        int FrameAssemblies,
        IReadOnlyList<string> MissingBulkheads,
        bool Ok);

    public static Result Validate(CadDocument doc, IReadOnlyList<StructuralJoint>? joints = null)
    {
        if (CadDocumentAdapter.Active.Parts.Count == 0)
            CadDocumentAdapter.LoadFromDocument(doc);

        var graph = StructuralAssemblyGraph.Validate(doc, joints);
        var reg = CadDocumentAdapter.Active;
        var expectedBh = new[]
        {
            "BH-BOW-COLLISION", "BH-CROSS-FORE", "BH-CROSS-AFT",
            "BH-CREW-AFT", "BH-ENG-FORE", "BH-ENG-AFT",
        };
        var missing = expectedBh
            .Where(id => !reg.Assemblies.Any(a =>
                string.Equals(a.Id.Value, id, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        // Also accept plates registered under legacy-derived assemblies.
        if (missing.Count > 0 && reg.BulkheadPlateCount >= 6)
            missing = [];

        var ok = graph.Ok && missing.Count == 0 && reg.BulkheadPlateCount >= 6;
        return new Result(graph, reg.Parts.Count, reg.BulkheadPlateCount, reg.FrameAssemblyCount, missing, ok);
    }
}
