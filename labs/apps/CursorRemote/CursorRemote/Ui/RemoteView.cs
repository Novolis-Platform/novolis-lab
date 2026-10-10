using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CursorRemote.Protocol;
using CursorRemote.Services;

namespace CursorRemote.Ui;

public sealed class RemoteView : UserControl
{
    private const double MinimumZoom = 1;
    private const double MaximumZoom = 8;
    private const double TapSlop = 16;

    private readonly IRemoteControlSession _session;
    private readonly HostActivityLog _activityLog;
    private readonly HostShellOptions _shellOptions;
    private readonly TextBlock _status = RemoteTheme.Muted(string.Empty);
    private readonly TextBlock _connectStatus = RemoteTheme.Muted(string.Empty);
    private readonly TextBlock _hostDetails = RemoteTheme.Muted(string.Empty);
    private readonly TextBox _hostLog = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        FontFamily = RemotePalette.MonoFont,
        FontSize = 12,
        Foreground = RemotePalette.BodyBrush,
        Background = RemotePalette.PanelRaisedBrush,
        BorderBrush = RemotePalette.UncertainBrush,
        MinHeight = 220,
        MaxHeight = 320,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };
    private readonly CheckBox _closeToTray = new()
    {
        Content = "Close / minimize to tray",
        IsChecked = true,
        FontFamily = RemotePalette.BodyFont,
        FontSize = 14,
        Foreground = RemotePalette.BodyBrush,
    };
    private readonly Image _screenImage = new()
    {
        Stretch = Stretch.Uniform,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
    };
    private readonly Border _screenViewport = new()
    {
        Background = RemotePalette.PanelBrush,
        Padding = new Thickness(4),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        ClipToBounds = true,
        Child = new TextBlock
        {
            Text = "Connect to see the PC screen.",
            Foreground = RemotePalette.MutedBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };
    private readonly TextBox _hostEndpoint = RemoteTheme.TextBox();
    private readonly TextBox _textInput = RemoteTheme.TextBox("Type text into Cursor");
    private readonly StackPanel _connectPanel = new() { Spacing = 12 };
    private readonly StackPanel _hostList = new() { Spacing = 8 };
    private readonly Grid _remotePanel = new();
    private Bitmap? _screenBitmap;
    private DispatcherTimer? _refreshTimer;
    private double _zoom = 1;
    private double _panX;
    private double _panY;
    private double _pinchBaseZoom = 1;
    private double? _pinchStartDistance;
    private bool _gestureActive;
    private bool _panning;
    private Point? _pressPoint;
    private Point? _lastPanPoint;
    private DateTime _lastTapUtc;
    private readonly Dictionary<int, Point> _activePointers = new();
    private bool _scanning;
    private readonly TextBlock _zoomLabel = RemoteTheme.Muted("1.0×");

    public RemoteView(
        IRemoteControlSession session,
        HostActivityLog activityLog,
        HostShellOptions shellOptions)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _activityLog = activityLog ?? throw new ArgumentNullException(nameof(activityLog));
        _shellOptions = shellOptions ?? throw new ArgumentNullException(nameof(shellOptions));
        Background = RemotePalette.WindowBrush;
        Focusable = true;
        _session.Changed += OnSessionChanged;
        _activityLog.Changed += OnActivityLogChanged;
        _closeToTray.IsChecked = _shellOptions.CloseToTray;
        _closeToTray.IsCheckedChanged += (_, _) =>
            _shellOptions.CloseToTray = _closeToTray.IsChecked == true;

        if (_session.IsHost)
            Content = BuildHostPanel();
        else
            Content = BuildClientPanel();

        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
        UpdateUi();
        if (_session.IsHost)
            RefreshHostLog();
    }

    private Control BuildHostPanel()
    {
        _hostEndpoint.IsReadOnly = true;
        _hostEndpoint.FontFamily = RemotePalette.MonoFont;

        var copyEndpoint = RemoteTheme.Button("Copy endpoint", RemoteButtonKind.Secondary);
        copyEndpoint.Click += async (_, _) => await CopyAsync(_hostEndpoint.Text);

        var endpointRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children =
            {
                _hostEndpoint,
                copyEndpoint,
            },
        };
        Grid.SetColumn(copyEndpoint, 1);

        var exportLog = RemoteTheme.Button("Export log", RemoteButtonKind.Primary);
        exportLog.Click += async (_, _) => await ExportHostLogAsync();
        var clearLog = RemoteTheme.Button("Clear", RemoteButtonKind.Secondary);
        clearLog.Click += (_, _) =>
        {
            _activityLog.Clear();
            _activityLog.Info("Log cleared.");
        };
        var hideToTray = RemoteTheme.Button("Hide to tray", RemoteButtonKind.Quiet);
        hideToTray.Click += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is Window window)
            {
                _shellOptions.CloseToTray = true;
                _closeToTray.IsChecked = true;
                window.WindowState = WindowState.Minimized;
            }
        };

        var logActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { exportLog, clearLog, hideToTray },
        };

        var card = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                RemoteTheme.Label(ProductBrand.HostEyebrow),
                RemoteTheme.Title(ProductBrand.Name, 36),
                RemoteTheme.Body(ProductBrand.HostPitch),
                RemoteTheme.Label("LISTENING"),
                endpointRow,
                _status,
                _hostDetails,
                _closeToTray,
                RemoteTheme.Label("ACTIVITY"),
                logActions,
                _hostLog,
            },
        };

        return new Border
        {
            Background = RemotePalette.PanelBrush,
            Padding = new Thickness(28),
            Margin = new Thickness(24),
            MaxWidth = 900,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = new ScrollViewer
            {
                Content = card,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            },
        };
    }

    private Control BuildClientPanel()
    {
        var scan = RemoteTheme.Button("Scan LAN", RemoteButtonKind.Primary);
        scan.Click += async (_, _) => await ScanHostsAsync();

        _connectPanel.Children.Add(RemoteTheme.Label(ProductBrand.PhoneEyebrow));
        _connectPanel.Children.Add(RemoteTheme.Title(ProductBrand.Name, 36));
        _connectPanel.Children.Add(RemoteTheme.Body(ProductBrand.PhonePitch));
        _connectPanel.Children.Add(scan);
        _connectPanel.Children.Add(RemoteTheme.Label("HOSTS"));
        _connectPanel.Children.Add(_hostList);
        _connectPanel.Children.Add(_connectStatus);

        ConfigureScreenGestures();
        _remotePanel.RowDefinitions = new RowDefinitions("Auto,*,Auto");
        _remotePanel.RowSpacing = 8;

        var focus = RemoteTheme.Button("Focus Cursor", RemoteButtonKind.Primary);
        focus.Click += async (_, _) => await RunAsync(
            () => _session.FocusCursorAsync());
        var refresh = RemoteTheme.Button("Refresh", RemoteButtonKind.Secondary);
        refresh.Click += async (_, _) => await RunAsync(
            () => _session.RefreshAsync());
        var zoomOut = RemoteTheme.Button("−", RemoteButtonKind.Secondary);
        zoomOut.MinWidth = 44;
        zoomOut.Click += (_, _) => AdjustZoom(1 / 1.35);
        var zoomIn = RemoteTheme.Button("+", RemoteButtonKind.Secondary);
        zoomIn.MinWidth = 44;
        zoomIn.Click += (_, _) => AdjustZoom(1.35);
        var resetView = RemoteTheme.Button("Fit", RemoteButtonKind.Secondary);
        resetView.Click += (_, _) => ResetViewTransform();
        var disconnect = RemoteTheme.Button("Disconnect", RemoteButtonKind.Quiet);
        disconnect.Click += async (_, _) => await _session.DisconnectAsync();

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                focus,
                refresh,
                zoomOut,
                _zoomLabel,
                zoomIn,
                resetView,
                disconnect,
                _status,
            },
        };
        Grid.SetRow(toolbar, 0);
        _remotePanel.Children.Add(toolbar);

        Grid.SetRow(_screenViewport, 1);
        _remotePanel.Children.Add(_screenViewport);

        var controls = BuildInputPanel();
        Grid.SetRow(controls, 2);
        _remotePanel.Children.Add(controls);

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Thickness(16),
        };
        Grid.SetRow(_connectPanel, 0);
        root.Children.Add(new Border
        {
            Background = RemotePalette.PanelBrush,
            Padding = new Thickness(24),
            MaxWidth = 680,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _connectPanel,
        });
        Grid.SetRow(_remotePanel, 0);
        root.Children.Add(_remotePanel);
        Content = root;
        return root;
    }

    private void ConfigureScreenGestures()
    {
        // Avalonia's PinchGestureRecognizer is unreliable on Android once a pointer
        // is captured for pan. Track multi-touch distances ourselves instead.
        _screenViewport.Focusable = true;
        _screenViewport.IsHitTestVisible = true;
        _screenViewport.AddHandler(
            InputElement.PointerPressedEvent,
            OnScreenPointerPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        _screenViewport.AddHandler(
            InputElement.PointerMovedEvent,
            OnScreenPointerMoved,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        _screenViewport.AddHandler(
            InputElement.PointerReleasedEvent,
            OnScreenPointerReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        _screenViewport.AddHandler(
            InputElement.PointerCaptureLostEvent,
            OnScreenPointerCaptureLost,
            RoutingStrategies.Bubble);
        _screenViewport.PointerWheelChanged += OnScreenPointerWheel;
    }

    private Control BuildInputPanel()
    {
        var sendText = RemoteTheme.Button("Send text", RemoteButtonKind.Primary);
        sendText.Click += async (_, _) => await SendTextAsync();

        var textRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children =
            {
                _textInput,
                sendText,
            },
        };
        Grid.SetColumn(sendText, 1);

        var keyRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
        };
        foreach (var key in new[] { "Escape", "Tab", "Enter", "Control+Enter", "Control+S" })
        {
            var button = RemoteTheme.Button(key.Replace("Control+", "Ctrl+"), RemoteButtonKind.Secondary);
            button.Click += async (_, _) => await RunAsync(
                () => _session.SendKeyAsync(key));
            keyRow.Children.Add(button);
        }

        return new StackPanel
        {
            Spacing = 8,
            Children = { textRow, keyRow },
        };
    }

    private async Task ScanHostsAsync()
    {
        if (_scanning)
            return;

        _scanning = true;
        _connectStatus.Text = "Scanning LAN for Cursor Remote…";
        _hostList.Children.Clear();
        _hostList.Children.Add(RemoteTheme.Muted("Looking for hosts on port 18790…"));

        try
        {
            var hosts = await RemoteHostDiscovery.DiscoverAsync(TimeSpan.FromSeconds(2.5))
                .ConfigureAwait(true);
            _hostList.Children.Clear();
            if (hosts.Count == 0)
            {
                _hostList.Children.Add(RemoteTheme.Muted(
                    "No matching hosts. Start Cursor Remote on the PC and try again."));
                _connectStatus.Text = "No hosts found.";
                return;
            }

            foreach (var host in hosts)
            {
                var captured = host;
                var button = RemoteTheme.Button(
                    $"{captured.HostName}  ·  {captured.Endpoint}",
                    RemoteButtonKind.Secondary);
                button.HorizontalAlignment = HorizontalAlignment.Stretch;
                button.Click += async (_, _) => await ConnectToHostAsync(captured);
                _hostList.Children.Add(button);
            }

            _connectStatus.Text = $"{hosts.Count} host(s) ready.";
        }
        catch (Exception exception)
        {
            _hostList.Children.Clear();
            _connectStatus.Text = $"Scan failed: {exception.Message}";
        }
        finally
        {
            _scanning = false;
        }
    }

    private async Task ConnectToHostAsync(DiscoveredRemoteHost host)
    {
        _connectStatus.Text = $"Connecting to {host.HostName}…";
        await RunAsync(() => _session.ConnectAsync(host.Endpoint));
    }

    private void OnScreenPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        var point = args.GetPosition(_screenViewport);
        _activePointers[args.Pointer.Id] = point;

        if (_activePointers.Count >= 2)
        {
            _gestureActive = true;
            _panning = false;
            _pinchStartDistance = PointerDistance();
            _pinchBaseZoom = _zoom;
            args.Handled = true;
            return;
        }

        _gestureActive = false;
        _panning = false;
        _pressPoint = point;
        _lastPanPoint = point;
        // Capture only the single finger so moves keep arriving; second finger
        // still arrives as a separate pointer on Android.
        args.Pointer.Capture(_screenViewport);
        args.Handled = true;
    }

    private void OnScreenPointerMoved(object? sender, PointerEventArgs args)
    {
        if (!_activePointers.ContainsKey(args.Pointer.Id))
            return;

        var point = args.GetPosition(_screenViewport);
        _activePointers[args.Pointer.Id] = point;

        if (_activePointers.Count >= 2)
        {
            var distance = PointerDistance();
            if (_pinchStartDistance is null or <= 0)
            {
                _pinchStartDistance = distance;
                _pinchBaseZoom = _zoom;
            }
            else if (distance is { } measured && measured > 0)
            {
                SetZoom(
                    RemotePinchZoom.FromDistances(
                        _pinchBaseZoom,
                        _pinchStartDistance.Value,
                        measured,
                        MinimumZoom,
                        MaximumZoom),
                    commitPinchBase: false);
            }

            _gestureActive = true;
            _panning = false;
            args.Handled = true;
            return;
        }

        if (_zoom <= MinimumZoom || _lastPanPoint is null)
            return;

        var dx = point.X - _lastPanPoint.Value.X;
        var dy = point.Y - _lastPanPoint.Value.Y;
        _lastPanPoint = point;

        if (!_panning)
        {
            if ((dx * dx) + (dy * dy) <= TapSlop * TapSlop)
                return;
            _panning = true;
            _gestureActive = true;
        }

        _panX += dx;
        _panY += dy;
        ClampPan();
        ApplyViewTransform();
        args.Handled = true;
    }

    private async void OnScreenPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        _activePointers.Remove(args.Pointer.Id);
        if (args.Pointer.Captured == _screenViewport)
            args.Pointer.Capture(null);

        if (_activePointers.Count >= 2)
        {
            _pinchStartDistance = PointerDistance();
            _pinchBaseZoom = _zoom;
            args.Handled = true;
            return;
        }

        if (_activePointers.Count == 1)
        {
            // Dropped out of a pinch into one finger — resume pan from remaining finger.
            _pinchStartDistance = null;
            _pinchBaseZoom = _zoom;
            var remaining = _activePointers.Values.First();
            _lastPanPoint = remaining;
            _pressPoint = remaining;
            _panning = false;
            args.Handled = true;
            return;
        }

        var wasGesture = _gestureActive || _panning || _pinchStartDistance is not null;
        _pinchStartDistance = null;
        _panning = false;
        _lastPanPoint = null;
        _gestureActive = false;

        var frame = _session.Screen;
        var press = _pressPoint;
        _pressPoint = null;
        if (frame is null || wasGesture || press is null)
            return;

        var point = args.GetPosition(_screenViewport);
        var dx = point.X - press.Value.X;
        var dy = point.Y - press.Value.Y;
        if ((dx * dx) + (dy * dy) > TapSlop * TapSlop)
            return;

        var now = DateTime.UtcNow;
        if ((now - _lastTapUtc).TotalMilliseconds < 280)
        {
            _lastTapUtc = DateTime.MinValue;
            AdjustZoom(_zoom < 1.75 ? 2.5 / Math.Max(_zoom, 0.01) : 1 / Math.Max(_zoom, 0.01));
            args.Handled = true;
            return;
        }

        _lastTapUtc = now;

        if (!RemoteCoordinateMapper.TryMap(
                _screenViewport.Bounds.Width,
                _screenViewport.Bounds.Height,
                frame.Width,
                frame.Height,
                point.X,
                point.Y,
                out var x,
                out var y,
                _zoom,
                _panX,
                _panY))
        {
            return;
        }

        await RunAsync(() => _session.ClickAsync(x, y));
        args.Handled = true;
    }

    private void OnScreenPointerCaptureLost(object? sender, PointerCaptureLostEventArgs args)
    {
        _activePointers.Remove(args.Pointer.Id);
        if (_activePointers.Count == 0)
        {
            _panning = false;
            _lastPanPoint = null;
            _pressPoint = null;
            _pinchStartDistance = null;
            _gestureActive = false;
        }
    }

    private void OnScreenPointerWheel(object? sender, PointerWheelEventArgs args)
    {
        AdjustZoom(args.Delta.Y > 0 ? 1.15 : 1 / 1.15);
        args.Handled = true;
    }

    private double? PointerDistance()
    {
        if (_activePointers.Count < 2)
            return null;

        var points = _activePointers.Values.Take(2).ToArray();
        var dx = points[0].X - points[1].X;
        var dy = points[0].Y - points[1].Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private void AdjustZoom(double factor) =>
        SetZoom(_zoom * factor);

    private void SetZoom(double zoom, bool commitPinchBase = true)
    {
        _zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        if (commitPinchBase)
            _pinchBaseZoom = _zoom;
        if (_zoom <= MinimumZoom)
        {
            _panX = 0;
            _panY = 0;
        }
        else
        {
            ClampPan();
        }

        ApplyViewTransform();
    }

    private void ClampPan()
    {
        var frame = _session.Screen;
        var viewportWidth = _screenViewport.Bounds.Width;
        var viewportHeight = _screenViewport.Bounds.Height;
        if (frame is null
            || viewportWidth <= 0
            || viewportHeight <= 0
            || _zoom <= MinimumZoom)
        {
            _panX = 0;
            _panY = 0;
            return;
        }

        var fitScale = Math.Min(
            viewportWidth / frame.Width,
            viewportHeight / frame.Height);
        var scaledWidth = frame.Width * fitScale * _zoom;
        var scaledHeight = frame.Height * fitScale * _zoom;
        var maxPanX = Math.Max(0, (scaledWidth - viewportWidth) / 2);
        var maxPanY = Math.Max(0, (scaledHeight - viewportHeight) / 2);
        _panX = Math.Clamp(_panX, -maxPanX, maxPanX);
        _panY = Math.Clamp(_panY, -maxPanY, maxPanY);
    }

    private void ApplyViewTransform()
    {
        _screenImage.RenderTransform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(_zoom, _zoom),
                new TranslateTransform(_panX, _panY),
            },
        };
        _zoomLabel.Text = $"{_zoom:0.0}×";
    }

    private void ResetViewTransform()
    {
        _zoom = 1;
        _pinchBaseZoom = 1;
        _panX = 0;
        _panY = 0;
        ApplyViewTransform();
    }

    private async Task SendTextAsync()
    {
        var text = _textInput.Text;
        if (string.IsNullOrEmpty(text))
            return;

        await RunAsync(async () =>
        {
            await _session.SendTextAsync(text);
            _textInput.Text = string.Empty;
            await _session.RefreshAsync();
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _status.Text = $"Error: {exception.Message}";
            _connectStatus.Text = _status.Text;
        }
    }

    private async void OnAttached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (_session.IsHost)
            return;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _refreshTimer.Tick += async (_, _) =>
        {
            if (_session.IsConnected)
                await RunAsync(() => _session.RefreshAsync());
        };
        _refreshTimer.Start();

        if (!_session.IsConnected)
            await ScanHostsAsync();
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
    }

    private void OnSessionChanged(object? sender, EventArgs args) =>
        Dispatcher.UIThread.Post(UpdateUi);

    private void OnActivityLogChanged(object? sender, EventArgs args) =>
        Dispatcher.UIThread.Post(RefreshHostLog);

    private void RefreshHostLog()
    {
        if (!_session.IsHost)
            return;

        var text = _activityLog.ExportText();
        if (_hostLog.Text == text)
            return;

        _hostLog.Text = text;
        _hostLog.CaretIndex = text.Length;
    }

    private async Task ExportHostLogAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
        {
            _activityLog.Warn("Export failed: no storage provider.");
            return;
        }

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Cursor Remote log",
            SuggestedFileName = $"cursor-remote-{DateTime.Now:yyyyMMdd-HHmmss}.log",
            FileTypeChoices =
            [
                new FilePickerFileType("Log file")
                {
                    Patterns = ["*.log", "*.txt"],
                },
            ],
        }).ConfigureAwait(true);

        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync().ConfigureAwait(true);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(_activityLog.ExportText()).ConfigureAwait(true);
        _activityLog.Info($"Exported log to {file.Name}.");
    }

    private void UpdateUi()
    {
        _status.Text = _session.Status;
        _connectStatus.Text = _session.Status;
        if (_session.IsHost)
        {
            var connection = _session.Connection;
            _hostEndpoint.Text = connection?.Endpoint ?? "Starting host…";
            _hostDetails.Text = connection is null
                ? "Tailscale was not detected. Start Tailscale and restart this host."
                : $"Host {connection.HostName} · protocol {connection.ProtocolVersion}"
                  + (connection.TailscaleAddress is { Length: > 0 } address
                      ? $" · Tailscale {address}"
                      : string.Empty);
            return;
        }

        if (!_session.IsConnected)
            ResetViewTransform();

        _connectPanel.IsVisible = !_session.IsConnected;
        _remotePanel.IsVisible = _session.IsConnected;
        UpdateScreen();
    }

    private void UpdateScreen()
    {
        var frame = _session.Screen;
        if (frame is null)
        {
            _screenImage.Source = null;
            if (_screenViewport.Child is Image)
                _screenViewport.Child = new TextBlock
                {
                    Text = "Connect to see the PC screen.",
                    Foreground = RemotePalette.MutedBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
            return;
        }

        using var stream = new MemoryStream(frame.Png, writable: false);
        var next = new Bitmap(stream);
        _screenBitmap?.Dispose();
        _screenBitmap = next;
        _screenImage.Source = next;
        ApplyViewTransform();
        _screenViewport.Child = _screenImage;
    }

    private async Task CopyAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }
}
