namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>Classifies lock compartment ids into people-movement volume kinds.</summary>
internal static class VolumeCirculation
{
    public static VolumeCirculationKind Classify(string? compartmentId)
    {
        if (string.IsNullOrWhiteSpace(compartmentId))
            return VolumeCirculationKind.None;

        var id = compartmentId.Trim();
        if (IsShirtSleeves(id))
            return VolumeCirculationKind.ShirtSleeves;
        if (id.StartsWith("AIRLOCK_A_", StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, "AIRLOCK_A", StringComparison.OrdinalIgnoreCase))
            return VolumeCirculationKind.AirlockAnteroom;
        if (id.StartsWith("AIRLOCK_B_", StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, "AIRLOCK_B", StringComparison.OrdinalIgnoreCase)
            || (id.StartsWith("AIRLOCK_", StringComparison.OrdinalIgnoreCase)
                && !id.StartsWith("AIRLOCK_A_", StringComparison.OrdinalIgnoreCase)))
            return VolumeCirculationKind.Airlock;
        if (id.StartsWith("STAIRS_", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("ELEV_", StringComparison.OrdinalIgnoreCase)
            || id.Contains("STAIR", StringComparison.OrdinalIgnoreCase))
            return VolumeCirculationKind.Hybrid;

        return VolumeCirculationKind.None;
    }

    public static bool IsShirtSleeves(string id) =>
        string.Equals(id, "ACCESS", StringComparison.OrdinalIgnoreCase)
        || string.Equals(id, "CROSSING", StringComparison.OrdinalIgnoreCase)
        || string.Equals(id, "CORR_P", StringComparison.OrdinalIgnoreCase)
        || string.Equals(id, "CORR_S", StringComparison.OrdinalIgnoreCase);

    public static DrawingCategoryId CategoryFor(VolumeCirculationKind kind) => kind switch
    {
        VolumeCirculationKind.ShirtSleeves => DrawingCategoryId.ShirtSleeves,
        VolumeCirculationKind.AirlockAnteroom => DrawingCategoryId.AirlockAnteroom,
        VolumeCirculationKind.Airlock => DrawingCategoryId.Airlock,
        VolumeCirculationKind.Hybrid => DrawingCategoryId.Hybrid,
        _ => DrawingCategoryId.General,
    };

    public static string DisplayName(VolumeCirculationKind kind) => kind switch
    {
        VolumeCirculationKind.ShirtSleeves => "Shirt-sleeves",
        VolumeCirculationKind.AirlockAnteroom => "Airlock anteroom",
        VolumeCirculationKind.Airlock => "Airlock",
        VolumeCirculationKind.Hybrid => "Hybrid circulation",
        _ => "—",
    };
}
