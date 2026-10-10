namespace BridgeCommander.Bridge;

/// <summary>Turns bridge status lines into radio-style lines for TTS.</summary>
public static class BridgeVoiceNarration
{
    /// <summary>Formats a status line for spoken playback.</summary>
    public static string ForStatus(string? statusLine)
    {
        if (string.IsNullOrWhiteSpace(statusLine))
            return string.Empty;

        var text = statusLine.Trim();
        if (text.StartsWith("Helm:", StringComparison.OrdinalIgnoreCase))
            return "Helm, " + text["Helm:".Length..].Trim();
        if (text.StartsWith("Tactical:", StringComparison.OrdinalIgnoreCase))
            return "Tactical, " + text["Tactical:".Length..].Trim();
        if (text.StartsWith("Engineering:", StringComparison.OrdinalIgnoreCase))
            return "Engineering, " + text["Engineering:".Length..].Trim();
        if (text.StartsWith("Nav:", StringComparison.OrdinalIgnoreCase))
            return "Navigation, " + text["Nav:".Length..].Trim();
        if (text.StartsWith("Comms:", StringComparison.OrdinalIgnoreCase))
            return "Comms, " + text["Comms:".Length..].Trim();
        if (text.StartsWith("Admin:", StringComparison.OrdinalIgnoreCase))
            return "Administration, " + text["Admin:".Length..].Trim();

        return text;
    }
}
