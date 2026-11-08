using CalypsoCad.Model;

namespace CalypsoCad.Drawing;

internal static class PartRegisterBuilder
{
    public static IReadOnlyList<PartRegisterLine> Build(string subjectId, ShipPartRegistry registry)
    {
        var parts = SubjectGeometryResolver.CollectPhysicalParts(subjectId, registry);
        var lines = new List<PartRegisterLine>();
        foreach (var group in parts.GroupBy(p => p.Id.Value, StringComparer.OrdinalIgnoreCase))
        {
            var p = group.First();
            var extent = p.StationZStem is { } z0
                ? $"z≈{z0:0.##} m"
                : p.DeckUp is { } up ? $"up≈{up:0.##} m" : null;
            lines.Add(new PartRegisterLine(
                p.Id.Value,
                p.DisplayName,
                p.Assembly.Value,
                Material: null,
                Quantity: group.Count(),
                ExtentNote: extent,
                FabDrawingRef: p.FabPartId,
                MassKg: null));
        }

        return lines;
    }
}
