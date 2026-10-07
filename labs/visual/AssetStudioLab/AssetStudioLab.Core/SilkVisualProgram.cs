namespace AssetStudioLab;

/// <summary>Handshake payload for a future Novolis.Silk.Visual adapter. No Silk.NET types.</summary>
public sealed record SilkVisualProgram(
    string Name,
    IReadOnlyList<IrNode> Mixers,
    IReadOnlyDictionary<string, string> Bindings);
