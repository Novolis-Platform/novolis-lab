using CursorRemote.Protocol;

namespace CursorRemote.Services;

public interface IRemoteControlSession
{
    bool IsHost { get; }

    bool IsConnected { get; }

    string Status { get; }

    RemoteConnectionInfo? Connection { get; }

    RemoteScreenFrame? Screen { get; }

    event EventHandler? Changed;

    Task ConnectAsync(
        string endpoint,
        CancellationToken cancellationToken = default);

    Task RefreshAsync(CancellationToken cancellationToken = default);

    Task FocusCursorAsync(CancellationToken cancellationToken = default);

    Task ClickAsync(
        double x,
        double y,
        string button = "left",
        int clickCount = 1,
        CancellationToken cancellationToken = default);

    Task SendTextAsync(
        string text,
        CancellationToken cancellationToken = default);

    Task SendKeyAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}
