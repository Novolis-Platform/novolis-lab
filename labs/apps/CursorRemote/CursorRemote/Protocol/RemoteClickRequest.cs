using System.Text.Json;

namespace CursorRemote.Protocol;

public sealed record RemoteClickRequest(
    double X,
    double Y,
    string Button = "left",
    int ClickCount = 1);
