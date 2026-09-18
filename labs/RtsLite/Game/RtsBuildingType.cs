namespace RtsLite.Game;

internal enum RtsBuildingType
{
    ConstructionYard,
    PowerPlant,
    Barracks,
    OreRefinery,
    WarFactory,
}

internal static class RtsBuildingDefs
{
    public static int FootprintW(this RtsBuildingType type) => type switch
    {
        RtsBuildingType.ConstructionYard => 3,
        RtsBuildingType.PowerPlant => 1,
        RtsBuildingType.Barracks => 2,
        RtsBuildingType.OreRefinery => 2,
        RtsBuildingType.WarFactory => 2,
        _ => 1,
    };

    public static int FootprintH(this RtsBuildingType type) => type switch
    {
        RtsBuildingType.ConstructionYard => 3,
        RtsBuildingType.PowerPlant => 1,
        RtsBuildingType.Barracks => 2,
        RtsBuildingType.OreRefinery => 2,
        RtsBuildingType.WarFactory => 3,
        _ => 1,
    };

    public static string Label(this RtsBuildingType type) => type switch
    {
        RtsBuildingType.ConstructionYard => "Construction Yard",
        RtsBuildingType.PowerPlant => "Power Plant",
        RtsBuildingType.Barracks => "Barracks",
        RtsBuildingType.OreRefinery => "Ore Refinery",
        RtsBuildingType.WarFactory => "War Factory",
        _ => type.ToString(),
    };

    public static int HotkeyDigit(this RtsBuildingType type) => type switch
    {
        RtsBuildingType.ConstructionYard => 1,
        RtsBuildingType.PowerPlant => 2,
        RtsBuildingType.Barracks => 3,
        RtsBuildingType.OreRefinery => 4,
        RtsBuildingType.WarFactory => 5,
        _ => 0,
    };
}
