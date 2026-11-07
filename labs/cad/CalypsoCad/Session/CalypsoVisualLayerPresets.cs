namespace CalypsoCad.Session;

internal static class CalypsoVisualLayerPresets
{
    private static readonly (CalypsoVisualLayers Layers, string Label)[] OrbitCycle =
    [
        (CalypsoVisualLayers.PrimaryStructure, "primary structure"),
        (CalypsoVisualLayers.PrimaryPlusDecks, "primary + decks"),
        (CalypsoVisualLayers.PressureStructure, "pressure structure"),
        (CalypsoVisualLayers.CompleteArrangement, "complete arrangement"),
        (CalypsoVisualLayers.Dk001Plan, "DK-001 plan"),
        (CalypsoVisualLayers.StructureNoHull, "structure no OML"),
        (CalypsoVisualLayers.InteriorStructureNoHull, "interiors no OML"),
        (CalypsoVisualLayers.HoldCargo, "HOLD cargo"),
        (CalypsoVisualLayers.StructureOnlyCutaway, "structure+cutouts"),
        (CalypsoVisualLayers.FitoutCutaway, "fitout+cutouts"),
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
