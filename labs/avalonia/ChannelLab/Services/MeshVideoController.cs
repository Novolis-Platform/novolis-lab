using Avalonia.Threading;
using Novolis.Avalonia.Video;
using Novolis.Video.Rtc;

namespace ChannelLab.Services;

/// <summary>Maps ChannelSession SignalR ↔ <see cref="IRtcMeshSession"/> and drives VideoSurface tiles.</summary>
internal sealed class MeshVideoController : IAsyncDisposable
{
    readonly ChannelSession _session;
    readonly VideoSurface _localSurface;
    readonly Dictionary<string, VideoSurface> _remoteSurfaces = new(StringComparer.OrdinalIgnoreCase);
    readonly object _gate = new();
    IRtcMeshSession? _mesh;
    bool _wired;

    public MeshVideoController(ChannelSession session, VideoSurface localSurface)
    {
        _session = session;
        _localSurface = localSurface;
    }

    public IReadOnlyDictionary<string, VideoSurface> RemoteSurfaces
    {
        get
        {
            lock (_gate)
                return _remoteSurfaces.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        }
    }

    public event Action? SurfacesChanged;

    public bool IsVideoOn => _mesh?.IsInVideo == true;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_session.Nick is null || !_session.IsConnected)
            throw new InvalidOperationException("Connect before enabling video.");

        if (_mesh is not null)
            return;

        var mesh = new SipSorceryRtcMeshSession(_session.Nick);
        _mesh = mesh;
        Wire(mesh);

        try
        {
            await mesh.JoinVideoAsync(cancellationToken).ConfigureAwait(false);
            // Hub admits + fans out after local capture is ready (avoids dropped offers).
            await _session.SendSignalAsync("video-join", string.Empty, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await StopCoreAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await StopCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    void Wire(IRtcMeshSession mesh)
    {
        if (_wired)
            return;
        _wired = true;

        mesh.LocalSignal += OnLocalSignal;
        mesh.LocalFrame += OnLocalFrame;
        mesh.RemoteFrame += OnRemoteFrame;
        _session.SignalReceived += OnSignalReceived;
    }

    void Unwire()
    {
        if (!_wired || _mesh is null)
            return;
        _wired = false;
        _mesh.LocalSignal -= OnLocalSignal;
        _mesh.LocalFrame -= OnLocalFrame;
        _mesh.RemoteFrame -= OnRemoteFrame;
        _session.SignalReceived -= OnSignalReceived;
    }

    void OnLocalSignal(RtcSignalMessage message)
    {
        // video-join already sent from StartAsync before JoinVideoAsync emits; skip duplicate.
        if (message.Kind == RtcSignalKind.VideoJoin)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await _session.SendSignalAsync(
                    ToHubKind(message.Kind),
                    message.Payload,
                    message.ToNick).ConfigureAwait(false);
            }
            catch
            {
                // Chat must keep working if signaling fails mid-session.
            }
        });
    }

    void OnSignalReceived(SignalMessage signal)
    {
        var mesh = _mesh;
        if (mesh is null || !mesh.IsInVideo)
            return;

        var kind = FromHubKind(signal.Kind);
        if (kind is null)
            return;

        if (kind == RtcSignalKind.VideoPart)
            RemoveRemoteSurface(signal.FromNick);

        var msg = new RtcSignalMessage(kind.Value, signal.FromNick, signal.Payload, signal.ToNick);
        _ = mesh.HandleSignalAsync(msg);
    }

    void OnLocalFrame(VideoFrame frame) => _localSurface.Present(frame);

    void OnRemoteFrame(string nick, VideoFrame frame)
    {
        VideoSurface surface;
        lock (_gate)
        {
            if (!_remoteSurfaces.TryGetValue(nick, out surface!))
            {
                surface = new VideoSurface { Label = nick, MinHeight = 120, MinWidth = 160 };
                _remoteSurfaces[nick] = surface;
                Dispatcher.UIThread.Post(() => SurfacesChanged?.Invoke());
            }
        }

        surface.Present(frame);
    }

    void RemoveRemoteSurface(string nick)
    {
        VideoSurface? surface;
        lock (_gate)
        {
            if (!_remoteSurfaces.Remove(nick, out surface))
                return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            surface.Clear();
            SurfacesChanged?.Invoke();
        });
    }

    async Task StopCoreAsync(CancellationToken cancellationToken = default)
    {
        var mesh = _mesh;
        Unwire();
        _mesh = null;

        if (mesh is not null)
        {
            try
            {
                await mesh.PartVideoAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            try
            {
                await _session.SendSignalAsync("video-part", string.Empty, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            await mesh.DisposeAsync().ConfigureAwait(false);
        }

        _localSurface.Clear();
        lock (_gate)
        {
            foreach (var surface in _remoteSurfaces.Values)
                surface.Clear();
            _remoteSurfaces.Clear();
        }

        Dispatcher.UIThread.Post(() => SurfacesChanged?.Invoke());
    }

    public async ValueTask DisposeAsync() => await StopCoreAsync().ConfigureAwait(false);

    static string ToHubKind(RtcSignalKind kind) => kind switch
    {
        RtcSignalKind.VideoJoin => "video-join",
        RtcSignalKind.VideoPart => "video-part",
        RtcSignalKind.Offer => "offer",
        RtcSignalKind.Answer => "answer",
        RtcSignalKind.Ice => "ice",
        _ => kind.ToString().ToLowerInvariant(),
    };

    static RtcSignalKind? FromHubKind(string kind) => kind.Trim().ToLowerInvariant() switch
    {
        "video-join" => RtcSignalKind.VideoJoin,
        "video-part" => RtcSignalKind.VideoPart,
        "offer" => RtcSignalKind.Offer,
        "answer" => RtcSignalKind.Answer,
        "ice" => RtcSignalKind.Ice,
        _ => null,
    };
}
