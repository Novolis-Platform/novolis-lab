using System.Text.Json;

namespace CursorRemote.Protocol;

public sealed record RemoteHelloDto(
    string AppId,
    string ProtocolVersion,
    string HostName,
    string[] Endpoints,
    int HttpPort,
    int DiscoveryPort);
