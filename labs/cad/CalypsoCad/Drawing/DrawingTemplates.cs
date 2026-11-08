using System.Numerics;

namespace CalypsoCad.Drawing;

/// <summary>
/// Declarative view specifications — replace category-specific geometry reconstruction.
/// </summary>
internal static class DrawingTemplates
{
    public static DrawingRequest AssemblyStandard(
        string subjectId,
        string? drawingNumber = null,
        string? title = null,
        StandardViewOrientation principal = StandardViewOrientation.TransverseLookingAft)
    {
        return new DrawingRequest(
            subjectId,
            [
                new DrawingViewDefinition("A", DrawingViewKind.Orthographic, principal,
                    Title: "Principal orthographic"),
                new DrawingViewDefinition("B", DrawingViewKind.Orthographic, StandardViewOrientation.PlanSternLeft,
                    Title: "Plan reference"),
                new DrawingViewDefinition("C", DrawingViewKind.Orthographic, StandardViewOrientation.ProfileSternLeft,
                    Title: "Profile reference"),
            ],
            new DrawingReferenceSet(DrawingReferenceFlags.DefaultContext),
            new DimensionPolicy(DimensionPolicyKind.OverallExtents),
            DrawingNumber: drawingNumber,
            Title: title ?? $"Assembly · {subjectId}",
            Label: subjectId);
    }

    public static DrawingRequest KnuckleStringer(string knuckleAssemblyId, string? drawingNumber = null) =>
        new(
            knuckleAssemblyId,
            [
                new DrawingViewDefinition("A", DrawingViewKind.Orthographic, StandardViewOrientation.ProfileSternLeft,
                    Title: "Profile · knuckle path"),
                new DrawingViewDefinition("B", DrawingViewKind.Orthographic, StandardViewOrientation.PlanSternLeft,
                    Title: "Plan · knuckle path"),
                new DrawingViewDefinition("C", DrawingViewKind.Assembly, StandardViewOrientation.PlanSternLeft,
                    Title: "Segment / part register"),
            ],
            new DrawingReferenceSet(DrawingReferenceFlags.DefaultContext | DrawingReferenceFlags.StructuralStations),
            new DimensionPolicy(DimensionPolicyKind.OverallPlusStations),
            DrawingNumber: drawingNumber,
            Title: $"Knuckle stringer · {knuckleAssemblyId}",
            Label: knuckleAssemblyId);

    public static DrawingRequest RingFrame(string frameAssemblyId, float zStem, string? drawingNumber = null) =>
        new(
            frameAssemblyId,
            [
                new DrawingViewDefinition("A", DrawingViewKind.Orthographic, StandardViewOrientation.TransverseLookingAft,
                    Title: $"Transverse · looking aft · z={zStem:0.##}"),
                new DrawingViewDefinition("B", DrawingViewKind.Orthographic, StandardViewOrientation.PlanSternLeft,
                    Title: "Plan location"),
                new DrawingViewDefinition("C", DrawingViewKind.Orthographic, StandardViewOrientation.ProfileSternLeft,
                    Title: "Profile location"),
            ],
            new DrawingReferenceSet(DrawingReferenceFlags.DefaultContext, ExtraStationsZStem: [zStem]),
            new DimensionPolicy(DimensionPolicyKind.OverallExtents),
            DrawingNumber: drawingNumber,
            Title: $"Ring frame · {frameAssemblyId}",
            Label: frameAssemblyId);

    public static DrawingRequest HeavyBulkhead(string bulkheadId, float zStem, string? drawingNumber = null) =>
        new(
            bulkheadId,
            [
                new DrawingViewDefinition("A", DrawingViewKind.Orthographic, StandardViewOrientation.TransverseLookingAft,
                    Title: $"Transverse plate · looking aft · z={zStem:0.##}"),
                new DrawingViewDefinition("B", DrawingViewKind.Orthographic, StandardViewOrientation.PlanSternLeft,
                    Title: "Plan station"),
                new DrawingViewDefinition("C", DrawingViewKind.Orthographic, StandardViewOrientation.ProfileSternLeft,
                    Title: "Profile station"),
            ],
            new DrawingReferenceSet(DrawingReferenceFlags.DefaultContext, ExtraStationsZStem: [zStem]),
            new DimensionPolicy(DimensionPolicyKind.OverallExtents),
            DrawingNumber: drawingNumber,
            Title: $"Heavy bulkhead · {bulkheadId}",
            Label: bulkheadId);

    public static DrawingRequest DeckPlan(string deckAssemblyId, string? drawingNumber = null) =>
        new(
            deckAssemblyId,
            [
                new DrawingViewDefinition("A", DrawingViewKind.Orthographic, StandardViewOrientation.PlanSternLeft,
                    Title: "Plan · deck panels"),
                new DrawingViewDefinition("B", DrawingViewKind.Orthographic, StandardViewOrientation.PlanSternLeft,
                    Title: "Whole-ship plan context"),
                new DrawingViewDefinition("C", DrawingViewKind.Orthographic, StandardViewOrientation.ProfileSternLeft,
                    Title: "Profile · deck elevation"),
            ],
            new DrawingReferenceSet(DrawingReferenceFlags.DefaultContext | DrawingReferenceFlags.DeckElevations),
            new DimensionPolicy(DimensionPolicyKind.OverallExtents),
            DrawingNumber: drawingNumber,
            Title: $"Deck · {deckAssemblyId}",
            Label: deckAssemblyId);

    public static DrawingRequest ArbitrarySection(
        string subjectId,
        float zStem,
        string? drawingNumber = null)
    {
        var plane = DrawingPlane.FromPointNormal(new Vector3(0, 0, zStem), Vector3.UnitZ);
        return new DrawingRequest(
            subjectId,
            [
                new DrawingViewDefinition("A", DrawingViewKind.Section, StandardViewOrientation.TransverseLookingAft,
                    SectionPlane: plane,
                    Title: $"Section · z={zStem:0.##} · looking aft"),
                new DrawingViewDefinition("B", DrawingViewKind.Orthographic, StandardViewOrientation.PlanSternLeft,
                    Title: "Section location · plan"),
                new DrawingViewDefinition("C", DrawingViewKind.Orthographic, StandardViewOrientation.ProfileSternLeft,
                    Title: "Section location · profile"),
            ],
            new DrawingReferenceSet(DrawingReferenceFlags.DefaultContext, ExtraStationsZStem: [zStem]),
            new DimensionPolicy(DimensionPolicyKind.OverallExtents),
            DrawingNumber: drawingNumber,
            Title: $"Section · {subjectId} @ z={zStem:0.##}",
            Label: $"{subjectId}@z{zStem:0.#}");
    }
}
