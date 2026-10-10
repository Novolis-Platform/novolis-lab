using CursorRemote.Protocol;
using CursorRemote.Services;

namespace CursorRemote.Desktop.Host;

public sealed class DesktopRemoteControlSession : IRemoteControlSession
{
    private readonly CursorRemoteHost _host;

    public DesktopRemoteControlSession(CursorRemoteHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _host.Changed += OnHostChanged;
    }

    public bool IsHost => true;

    public bool IsConnected => _host.IsRunning;

    public string Status => _host.Status;

    public RemoteConnectionInfo? Connection => _host.Connection;

    public RemoteScreenFrame? Screen => null;

    public event EventHandler? Changed;

    public Task ConnectAsync(
        string endpoint,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The Windows instance is the remote host.");

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Refresh is available from the Android controller.");

    public Task FocusCursorAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Focus is available from the Android controller.");

    public Task ClickAsync(
        double x,
        double y,
        string button = "left",
        int clickCount = 1,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Input is available from the Android controller.");

    public Task SendTextAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Input is available from the Android controller.");

    public Task SendKeyAsync(
        string key,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Input is available from the Android controller.");

    public Task DisconnectAsync() =>
        throw new NotSupportedException("The Windows host is managed by the application.");

    private void OnHostChanged(object? sender, EventArgs args) =>
        Changed?.Invoke(this, args);
}
