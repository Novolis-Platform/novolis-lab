namespace CalypsoCad.Services;

internal static class CalypsoVisualLayerPresets
{
    private static readonly (CalypsoVisualLayers Layers, string Label)[] OrbitCycle =
    [
        (CalypsoVisualLayers.DefaultOrbit, "all"),
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
