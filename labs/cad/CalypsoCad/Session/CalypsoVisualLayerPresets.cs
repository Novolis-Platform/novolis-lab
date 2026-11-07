namespace CalypsoCad.Session;

internal static class CalypsoVisualLayerPresets
{
    private static readonly (CalypsoVisualLayers Layers, string Label)[] OrbitCycle =
    [
        (CalypsoVisualLayers.OuterSkeleton, "0 outer skeleton (rings+skin longs)"),
        (CalypsoVisualLayers.CriticalStructure, "1 critical (BH+frames+deckplates)"),
        (CalypsoVisualLayers.PrimaryStructure, "2 primary steel (frames+BH+girders)"),
        (CalypsoVisualLayers.PrimaryPlusDecks, "3 + sandwich decks"),
        (CalypsoVisualLayers.PrimaryPlusDecks | CalypsoVisualLayers.Joinery, "4 + joinery stubs"),
        (CalypsoVisualLayers.PressureStructure | CalypsoVisualLayers.Airlocks, "5 pressure + airlocks"),
        (CalypsoVisualLayers.PrimaryPlusDecks | CalypsoVisualLayers.SpaceFinish | CalypsoVisualLayers.PassagewayCutouts,
            "6 passageway volumes"),
        (CalypsoVisualLayers.PrimaryPlusDecks | CalypsoVisualLayers.Partitions | CalypsoVisualLayers.Openings
         | CalypsoVisualLayers.Airlocks, "7 partitions + openings + airlocks"),
        (CalypsoVisualLayers.CompleteArrangement, "8 complete arrangement"),
        (CalypsoVisualLayers.Dk001Plan, "DK-001 plan"),
        (CalypsoVisualLayers.StructureNoHull, "structure no OML"),
        (CalypsoVisualLayers.HoldCargo, "HOLD cargo"),
        (CalypsoVisualLayers.HullShell | CalypsoVisualLayers.Structure, "shell+frames"),
    ];

    public static CalypsoVisualLayers CycleOrbit(int index, out string label)
    {
        var i = ((index % OrbitCycle.Length) + OrbitCycle.Length) % OrbitCycle.Length;
        label = OrbitCycle[i].Label;
        return OrbitCycle[i].Layers;
    }

    public static int OrbitPresetCount => OrbitCycle.Length;

    public static CalypsoVisualLayers ToggleInteriorCorridor(CalypsoVisualLayers current, out string label)
    {
        if (current == CalypsoVisualLayers.CorridorStructure)
        {
            label = "fitout";
            return CalypsoVisualLayers.DefaultInterior;
        }

        label = "corridor+structure";
        return CalypsoVisualLayers.CorridorStructure;
    }
}
