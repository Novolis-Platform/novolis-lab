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

public sealed record RemoteHelloDto(
    string AppId,
    string ProtocolVersion,
    string HostName,
    string[] Endpoints,
    int HttpPort,
    int DiscoveryPort);

public sealed record RemoteConnectionInfo(
    string Endpoint,
    string? TailscaleAddress,
    string ProtocolVersion,
    string HostName);

public sealed record RemoteStatusDto(
    string ProtocolVersion,
    string HostName,
    string[] Endpoints,
    int ScreenWidth,
    int ScreenHeight,
    int OriginX,
    int OriginY,
    bool CursorRunning);

public sealed record RemoteClickRequest(
    double X,
    double Y,
    string Button = "left",
    int ClickCount = 1);

public sealed record RemoteTextRequest(string Text);

public sealed record RemoteKeyRequest(string Key);

public sealed record RemoteScreenFrame(
    byte[] Png,
    int Width,
    int Height,
    int OriginX = 0,
    int OriginY = 0);

public sealed record RemoteOperationResponse(
    bool Ok,
    string Message);

public sealed record DiscoveredRemoteHost(
    string HostName,
    string Endpoint,
    string ProtocolVersion,
    string Source);
