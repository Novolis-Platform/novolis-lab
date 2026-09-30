namespace BridgeCommander.Bridge.Mcp;

public sealed record BridgeQaStepResult(
    string Prompt,
    bool Passed,
    string StatusLine,
    BridgeSnapshot Snapshot);
