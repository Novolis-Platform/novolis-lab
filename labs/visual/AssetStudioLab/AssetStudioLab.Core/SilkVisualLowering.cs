namespace AssetStudioLab;

/// <summary>Lowers IR to mixer ops Silk can consume without knowing EdgeWear.</summary>
public static class SilkVisualLowering
{
    public static SilkVisualProgram Lower(IrDefinition definition) =>
        new(definition.Name, definition.Nodes, definition.Outputs);
}
