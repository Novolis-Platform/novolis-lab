using CalypsoCad.Constraints;
using CalypsoCad.Model;

namespace CalypsoCad.Validation;

internal static class PressureValidation
{
    public static IReadOnlyList<string> CheckPressureBulkheads(
        DesignConstraints constraints,
        ShipPartRegistry registry)
    {
        var failures = new List<string>();
        foreach (var st in constraints.BulkheadStations.Where(s => s.PressureTight))
        {
            var hasPlate = registry.Parts.Any(p =>
                p.Kind == CadPartKind.BulkheadPlate
                && p.Functions.HasFlag(PartFunction.PressureTight)
                && p.StationZStem is { } z
                && MathF.Abs(z - st.ZStem) < 0.35f);
            if (!hasPlate)
                failures.Add($"PressureTight station {st.Id} @ {st.ZStem:0.##} has no pressure bulkhead plate");
        }

        return failures;
    }
}
