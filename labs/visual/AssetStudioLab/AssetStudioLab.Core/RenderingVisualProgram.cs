namespace AssetStudioLab;

/// <summary>Handshake payload for a future Novolis.Rendering.Visual adapter.</summary>
public sealed record RenderingVisualProgram(
    string Name,
    VisualDefinitionKind Kind,
    IReadOnlyDictionary<string, string> Fields);
