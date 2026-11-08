namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>
/// People-movement volume classes for the drawing package
/// (shirt-sleeves passageway, airlock vestibule/chamber, hybrid vertical circulation).
/// </summary>
internal enum VolumeCirculationKind
{
    /// <summary>Not a people-movement volume (cabins, hold, plant, etc.).</summary>
    None = 0,

    /// <summary>Shirt-sleeves passageway spine — ACCESS + CROSSING + CORR_P/S.</summary>
    ShirtSleeves = 1,

    /// <summary>Airlock vestibule / anteroom — <c>AIRLOCK_A_*</c>.</summary>
    AirlockAnteroom = 2,

    /// <summary>Outer airlock chamber — <c>AIRLOCK_B_*</c>.</summary>
    Airlock = 3,

    /// <summary>Hybrid people movement — stairs / elevators spanning decks.</summary>
    Hybrid = 4,
}
