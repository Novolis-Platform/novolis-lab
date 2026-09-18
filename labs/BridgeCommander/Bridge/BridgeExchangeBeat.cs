namespace BridgeCommander.Bridge;

/// <summary>One beat in the scripted bridge exchange (display + optional voice + optional order).</summary>
public sealed record BridgeExchangeBeat(
    BridgeCharacter Character,
    string Display,
    string? VoiceLine = null,
    string? Transmit = null,
    int PauseAfterMs = 400)
{
    /// <summary>Speaker display name (for logs and compatibility).</summary>
    public string Speaker => Character.DisplayName;

    /// <summary>Narration-only beat (no command).</summary>
    public static BridgeExchangeBeat Say(
        BridgeCharacter character,
        string line,
        int pauseMs = 600) =>
        new(character, line, VoiceLine: line, PauseAfterMs: pauseMs);

    /// <summary>Captain order plus station acknowledgment (two beats).</summary>
    public static BridgeExchangeBeat[] OrderWithAck(
        string captainLine,
        string transmit,
        BridgeCharacter station,
        string ackLine,
        int pauseMs = 350) =>
    [
        new(
            BridgeCharacterRegistry.Captain,
            captainLine,
            VoiceLine: captainLine,
            Transmit: transmit,
            PauseAfterMs: 200),
        Say(station, ackLine, pauseMs),
    ];
}
