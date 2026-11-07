namespace CalypsoCad.Session;

internal static class CalypsoVisualLayerPresets
{
    private static readonly (CalypsoVisualLayers Layers, string Label)[] OrbitCycle =
    [
        (CalypsoVisualLayers.PrimaryStructure, "1 primary steel (frames+BH+girders)"),
        (CalypsoVisualLayers.PrimaryPlusDecks, "2 + sandwich decks"),
        (CalypsoVisualLayers.PrimaryPlusDecks | CalypsoVisualLayers.Joinery, "3 + joinery stubs"),
        (CalypsoVisualLayers.PressureStructure | CalypsoVisualLayers.Airlocks, "4 pressure + airlocks"),
        (CalypsoVisualLayers.PrimaryPlusDecks | CalypsoVisualLayers.SpaceFinish | CalypsoVisualLayers.PassagewayCutouts,
            "5 passageway volumes"),
        (CalypsoVisualLayers.PrimaryPlusDecks | CalypsoVisualLayers.Partitions | CalypsoVisualLayers.Openings
         | CalypsoVisualLayers.Airlocks, "6 partitions + openings + airlocks"),
        (CalypsoVisualLayers.CompleteArrangement, "7 complete arrangement"),
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
