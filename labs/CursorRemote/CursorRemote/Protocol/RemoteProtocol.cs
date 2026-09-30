using System.Text.Json;

namespace CursorRemote.Protocol;

public static class RemoteProtocol
{
    public const string AppId = "CursorRemote";
    public const string Version = "1.2";
    public const int DefaultPort = 18790;
    public const int DiscoveryPort = 18791;
    public const string DiscoveryWho = "CURSORREMOTE-WHO";

    public static JsonSerializerOptions JsonOptions { get; } =
        new(JsonSerializerDefaults.Web);

    public static bool IsCompatible(string? protocolVersion)
    {
        if (string.IsNullOrWhiteSpace(protocolVersion))
            return false;
        // Same major line (1.x) is enough for this personal app.
        var major = protocolVersion.Split('.', 2)[0];
        return major == Version.Split('.', 2)[0];
    }
}
