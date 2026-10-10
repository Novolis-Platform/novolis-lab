using System.Text.Json;

namespace CursorRemote.Protocol;

public sealed record DiscoveredRemoteHost(
    string HostName,
    string Endpoint,
    string ProtocolVersion,
    string Source);
