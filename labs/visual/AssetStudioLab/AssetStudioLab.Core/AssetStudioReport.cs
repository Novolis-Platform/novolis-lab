namespace AssetStudioLab;

/// <summary>Headless compile / bake / compose run used by CI and <c>--headless</c>.</summary>
public sealed record AssetStudioReport(
    bool Success,
    string Source,
    string Stack,
    string Csharp,
    VisualIr Ir,
    SilkVisualProgram? Silk,
    float BeamLuminance,
    int PipelineExit,
    IReadOnlyList<string> TimelineRows,
    string Message);
