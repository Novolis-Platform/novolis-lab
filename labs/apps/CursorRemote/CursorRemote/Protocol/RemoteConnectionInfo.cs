using System.Text.Json;

namespace CursorRemote.Protocol;

public sealed record RemoteConnectionInfo(
    string Endpoint,
    string? TailscaleAddress,
    string ProtocolVersion,
    string HostName);
