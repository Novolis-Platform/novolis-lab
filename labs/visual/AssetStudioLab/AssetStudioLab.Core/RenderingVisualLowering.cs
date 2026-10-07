namespace AssetStudioLab;

/// <summary>Lowers IR to authoring fields Rendering can compile on its own island.</summary>
public static class RenderingVisualLowering
{
    public static RenderingVisualProgram Lower(IrDefinition definition) =>
        new(definition.Name, definition.Kind, definition.Outputs);
}
