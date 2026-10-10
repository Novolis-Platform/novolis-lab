using System.Text.Json;

namespace CursorRemote.Protocol;

public sealed record RemoteOperationResponse(
    bool Ok,
    string Message);
