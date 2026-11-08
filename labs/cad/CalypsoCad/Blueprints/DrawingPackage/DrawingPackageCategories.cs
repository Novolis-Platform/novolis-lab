namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>Fixed DNV/ISO-style category tree for the Calypso drawing package.</summary>
internal static class DrawingPackageCategories
{
    public static IReadOnlyList<DrawingCategorySpec> All { get; } =
    [
        new(DrawingCategoryId.General, "100-general", "General arrangement", "GA",
            "Package cover, drawing list, and cross-references (ISO 7200 / class-society register style)."),
        new(DrawingCategoryId.Bulkheads, "200-structure/210-bulkheads", "Bulkheads", "Structure",
            "Heavy transverse bulkhead plates — numbered BH schedule stations."),
        new(DrawingCategoryId.Frames, "200-structure/220-frames", "Frames", "Structure",
            "Ordinary transverse web frames at bay stations."),
        new(DrawingCategoryId.DeckPlates, "200-structure/230-deck-plates", "Deck plates", "Structure",
            "Cellular sandwich deck face plates by deck."),
        new(DrawingCategoryId.StructuralMembers, "200-structure/240-structural-members", "Structural members", "Structure",
            "Longitudinals, girders, coamings, and related primary members."),
        new(DrawingCategoryId.KnuckleStringers, "200-structure/250-knuckle-stringers", "Knuckle stringers", "Structure",
            "Eight continuous outer-hull plating knuckle paths — swept segment geometry."),
        new(DrawingCategoryId.ShirtSleeves, "300-volumes/310-shirt-sleeves", "Shirt-sleeves", "Volumes",
            "Habitable passageway spine: ACCESS + CROSSING + CORR_P/S."),
        new(DrawingCategoryId.AirlockAnteroom, "300-volumes/320-airlock-anteroom", "Airlock anteroom", "Volumes",
            "Airlock vestibules (AIRLOCK_A) — people movement into the lock."),
        new(DrawingCategoryId.Airlock, "300-volumes/330-airlock", "Airlock", "Volumes",
            "Outer airlock chambers (AIRLOCK_B) — vacuum-barrier side."),
        new(DrawingCategoryId.Hybrid, "300-volumes/340-hybrid", "Hybrid circulation", "Volumes",
            "Vertical people movement: stairs and elevators spanning decks."),
        new(DrawingCategoryId.Bom, "900-bom", "Bills of materials", "BOM",
            "Master and category BOMs (structure plates + volume schedule)."),
    ];

    public static DrawingCategorySpec Get(DrawingCategoryId id) =>
        All.First(c => c.Id == id);
}
