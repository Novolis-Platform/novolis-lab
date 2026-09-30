using System.Text.Json;

namespace CursorRemote.Protocol;

public sealed record RemoteStatusDto(
    string ProtocolVersion,
    string HostName,
    string[] Endpoints,
    int ScreenWidth,
    int ScreenHeight,
    int OriginX,
    int OriginY,
    bool CursorRunning);
