using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ChannelLab.Services;
using ChannelLab.Ui;
using Novolis.Avalonia.Video;

namespace ChannelLab.Windows;

internal sealed class PeerWindow : Window
{
    readonly ChannelSession _session = new();
    readonly TextBox _nickBox = new() { PlaceholderText = "nick", Width = 140 };
    readonly TextBlock _status = new()
    {
        FontFamily = ChannelPalette.Body,
        FontSize = 12,
        Foreground = ChannelPalette.InkMutedBrush,
        Text = "Disconnected",
    };
    readonly ListBox _channels = new();
    readonly ListBox _roster = new();
    readonly ItemsControl _buffer = new();
    readonly TextBox _composer = new() { PlaceholderText = "Message #lobby", IsEnabled = false };
    readonly Button _connectButton;
    readonly Button _videoButton;
    readonly ScrollViewer _bufferScroll = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    readonly VideoSurface _localSurface = new() { Label = "you", MinHeight = 120, MinWidth = 160 };
    readonly StackPanel _videoStrip = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    readonly Border _videoHost;
    MeshVideoController? _video;

    public PeerWindow(string? suggestedNick = null)
    {
        Title = "ChannelLab peer";
        Width = 920;
        Height = 640;
        MinWidth = 720;
        MinHeight = 480;
        Background = ChannelPalette.NavyDeepBrush;
        if (!string.IsNullOrWhiteSpace(suggestedNick))
            _nickBox.Text = suggestedNick;

        _connectButton = PrimaryButton("Connect", OnConnectClicked);
        _videoButton = PrimaryButton("Video", OnVideoClicked);
        _videoButton.IsEnabled = false;
        _composer.KeyDown += OnComposerKeyDown;

        _channels.ItemsSource = new[] { "#lobby" };
        _channels.SelectedIndex = 0;

        _buffer.ItemsSource = new List<Control>();
        _bufferScroll.Content = _buffer;

        _videoStrip.Children.Add(_localSurface);
        _videoHost = new Border
        {
            Background = ChannelPalette.PanelBrush,
            BorderBrush = ChannelPalette.EdgeBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 8),
            Height = 148,
            IsVisible = false,
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = _videoStrip,
            },
        };

        Content = BuildChrome();

        _session.StatusChanged += s => Dispatcher.UIThread.Post(() => _status.Text = s);
        _session.MessageReceived += m => Dispatcher.UIThread.Post(() => AppendMessage(m));
        _session.HistoryReceived += list => Dispatcher.UIThread.Post(() => ReplaceHistory(list));
        _session.RosterChanged += nicks => Dispatcher.UIThread.Post(() =>
        {
            _roster.ItemsSource = nicks.ToList();
        });
    }

    Control BuildChrome()
    {
        var header = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("*,Auto"),
            Margin = new Thickness(16, 12),
        };
        header.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = "ChannelLab",
                    FontFamily = ChannelPalette.Display,
                    FontSize = 22,
                    FontWeight = FontWeight.Bold,
                    Foreground = ChannelPalette.MistSoftBrush,
                },
                _status,
            },
        });
        var nickRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _nickBox, _connectButton, _videoButton },
        };
        Grid.SetColumn(nickRow, 1);
        header.Children.Add(nickRow);

        var body = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("160,*,180"),
            Margin = new Thickness(12, 0, 12, 12),
            RowDefinitions = RowDefinitions.Parse("*"),
        };

        body.Children.Add(Panel("Channels", _channels));
        var center = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_composer, Dock.Bottom);
        DockPanel.SetDock(_videoHost, Dock.Top);
        _composer.Margin = new Thickness(0, 8, 0, 0);
        center.Children.Add(_composer);
        center.Children.Add(_videoHost);
        center.Children.Add(new Border
        {
            Background = ChannelPalette.PanelBrush,
            BorderBrush = ChannelPalette.EdgeBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Child = _bufferScroll,
        });
        Grid.SetColumn(center, 1);
        center.Margin = new Thickness(8, 0);
        body.Children.Add(center);
        var names = Panel("Names", _roster);
        Grid.SetColumn(names, 2);
        body.Children.Add(names);

        var headerBorder = new Border
        {
            Background = ChannelPalette.NavyBrush,
            Child = header,
        };
        DockPanel.SetDock(headerBorder, Dock.Top);

        return new DockPanel
        {
            LastChildFill = true,
            Children = { headerBorder, body },
        };
    }

    static Border Panel(string title, Control content)
    {
        var titleBlock = new TextBlock
        {
            Text = title,
            FontFamily = ChannelPalette.Body,
            FontSize = 12,
            Foreground = ChannelPalette.CopperBrush,
            Margin = new Thickness(0, 0, 0, 8),
        };
        DockPanel.SetDock(titleBlock, Dock.Top);
        return new Border
        {
            Background = ChannelPalette.PanelBrush,
            BorderBrush = ChannelPalette.EdgeBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Child = new DockPanel
            {
                LastChildFill = true,
                Children = { titleBlock, content },
            },
        };
    }

    async void OnConnectClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var nick = _nickBox.Text?.Trim() ?? string.Empty;
        if (nick.Length < 2)
        {
            _status.Text = "Pick a nick (2+ chars).";
            return;
        }

        _connectButton.IsEnabled = false;
        try
        {
            await _session.ConnectAsync(nick).ConfigureAwait(true);
            Title = $"ChannelLab — {nick}";
            _composer.IsEnabled = true;
            _nickBox.IsEnabled = false;
            _connectButton.Content = "Connected";
            _videoButton.IsEnabled = true;
            _localSurface.Label = nick;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _connectButton.IsEnabled = true;
        }
    }

    async void OnVideoClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_session.IsConnected)
            return;

        _videoButton.IsEnabled = false;
        try
        {
            if (_video?.IsVideoOn == true)
            {
                await _video.StopAsync().ConfigureAwait(true);
                await _video.DisposeAsync().ConfigureAwait(true);
                _video = null;
                _videoHost.IsVisible = false;
                _videoButton.Content = "Video";
                RebuildVideoStrip();
                return;
            }

            _video = new MeshVideoController(_session, _localSurface);
            _video.SurfacesChanged += () => Dispatcher.UIThread.Post(RebuildVideoStrip);
            await _video.StartAsync().ConfigureAwait(true);
            _videoHost.IsVisible = true;
            _videoButton.Content = "Video off";
            RebuildVideoStrip();
        }
        catch (Exception ex)
        {
            _status.Text = $"Video failed: {ex.Message}";
            if (_video is not null)
            {
                try { await _video.DisposeAsync().ConfigureAwait(true); } catch { /* ignore */ }
                _video = null;
            }

            _videoHost.IsVisible = false;
            _videoButton.Content = "Video";
        }
        finally
        {
            _videoButton.IsEnabled = _session.IsConnected;
        }
    }

    void RebuildVideoStrip()
    {
        _videoStrip.Children.Clear();
        _videoStrip.Children.Add(_localSurface);
        if (_video is null)
            return;
        foreach (var pair in _video.RemoteSurfaces.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            _videoStrip.Children.Add(pair.Value);
    }

    async void OnComposerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None)
            return;
        e.Handled = true;
        var body = _composer.Text?.Trim() ?? string.Empty;
        if (body.Length == 0 || !_session.IsConnected)
            return;
        _composer.Text = string.Empty;
        try
        {
            await _session.SayAsync(body).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }

    void ReplaceHistory(IReadOnlyList<ChannelMessage> messages)
    {
        var items = new List<Control>();
        foreach (var message in messages)
            items.Add(FormatLine(message));
        _buffer.ItemsSource = items;
        ScrollToEnd();
    }

    void AppendMessage(ChannelMessage message)
    {
        var items = (_buffer.ItemsSource as List<Control>) ?? [];
        items.Add(FormatLine(message));
        _buffer.ItemsSource = null;
        _buffer.ItemsSource = items;
        ScrollToEnd();
    }

    static TextBlock FormatLine(ChannelMessage message) => new()
    {
        Text = $"[{message.At.ToLocalTime():HH:mm}] <{message.Nick}> {message.Body}",
        FontFamily = ChannelPalette.Mono,
        FontSize = 13,
        Foreground = ChannelPalette.MistBrush,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 4),
    };

    void ScrollToEnd() =>
        Dispatcher.UIThread.Post(() => _bufferScroll.ScrollToEnd(), DispatcherPriority.Background);

    static Button PrimaryButton(string label, EventHandler<Avalonia.Interactivity.RoutedEventArgs> handler)
    {
        var button = new Button
        {
            Content = label,
            Background = ChannelPalette.TealBrush,
            Foreground = ChannelPalette.MistSoftBrush,
            Padding = new Thickness(14, 8),
            FontFamily = ChannelPalette.Body,
        };
        button.Click += handler;
        return button;
    }

    protected override async void OnClosed(EventArgs e)
    {
        if (_video is not null)
        {
            try { await _video.DisposeAsync().ConfigureAwait(true); } catch { /* ignore */ }
            _video = null;
        }

        try
        {
            await _session.PartAsync().ConfigureAwait(true);
        }
        catch
        {
            // ignore
        }

        await _session.DisposeAsync().ConfigureAwait(true);
        base.OnClosed(e);
    }
}
