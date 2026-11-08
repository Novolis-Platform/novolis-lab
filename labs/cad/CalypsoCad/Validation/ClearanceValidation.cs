using CalypsoCad.Constraints;
using CalypsoCad.Model;

namespace CalypsoCad.Validation;

internal static class ClearanceValidation
{
    public static IReadOnlyList<string> CheckTransverseInCargo(
        DesignConstraints constraints,
        IEnumerable<CadPart> parts)
    {
        var failures = new List<string>();
        foreach (var p in parts)
        {
            if (p.Kind is not (CadPartKind.RingFrame or CadPartKind.WebFrame or CadPartKind.BulkheadPlate))
                continue;
            if (p.StationZStem is not { } z)
                continue;
            // Heavy BHs may sit on hold faces; ordinary webs inside cargo clearance are flagged.
            if (p.Kind == CadPartKind.BulkheadPlate)
                continue;
            if (constraints.InCargoClearance(z) && p.Kind == CadPartKind.WebFrame)
                failures.Add($"{p.Id.Value} web frame inside cargo clearance at z={z:0.##}");
        }

        return failures;
    }
}
