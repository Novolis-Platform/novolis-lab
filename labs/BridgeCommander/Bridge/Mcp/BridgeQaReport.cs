namespace BridgeCommander.Bridge.Mcp;

public sealed record BridgeQaReport(
    string Scenario,
    bool Passed,
    IReadOnlyList<BridgeQaStepResult> Steps);
