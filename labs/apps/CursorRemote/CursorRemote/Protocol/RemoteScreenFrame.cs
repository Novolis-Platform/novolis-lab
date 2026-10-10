using System.Text.Json;

namespace CursorRemote.Protocol;

public sealed record RemoteScreenFrame(
    byte[] Png,
    int Width,
    int Height,
    int OriginX = 0,
    int OriginY = 0);
