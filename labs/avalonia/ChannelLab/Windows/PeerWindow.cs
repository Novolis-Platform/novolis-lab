using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ChannelLab.Services;
using ChannelLab.Ui;
using Novolis.Avalonia.Chat;
using Novolis.Avalonia.Video;
using Novolis.Chat.Abstractions;

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
    readonly TextBlock _fingerprint = new()
    {
        FontFamily = ChannelPalette.Mono,
        FontSize = 11,
        Foreground = ChannelPalette.CopperBrush,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 420,
    };
    readonly TextBlock _peerFingerprint = new()
    {
        FontFamily = ChannelPalette.Mono,
        FontSize = 11,
        Foreground = ChannelPalette.InkMutedBrush,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 420,
    };
    readonly ChatRail _chatRail = new();
    readonly ChatPresenceList _presence = new();
    readonly ChatThreadPanel _thread = new();
    readonly ListBox _groups = new();
    readonly List<ChatMessageDto> _messages = [];
    readonly TextBox _composer = new() { PlaceholderText = "Select and trust a peer", IsEnabled = false };
    readonly TextBox _groupNameBox = new() { PlaceholderText = "group name", Width = 120 };
    readonly TextBox _groupMembersBox = new() { PlaceholderText = "members: alice, bob" };
    readonly Button _connectButton;
    readonly Button _trustButton;
    readonly Button _videoButton;
    readonly Button _muteButton;
    readonly Button _createGroupButton;
    readonly Button _approveGroupButton;
    readonly VideoSurface _localSurface = new() { Label = "you", MinHeight = 120, MinWidth = 160 };
    readonly StackPanel _videoStrip = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    readonly Border _videoHost;
    MeshVideoController? _video;
    string? _selectedPeer;
    SecureTextGroup? _selectedGroup;

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
        _trustButton = PrimaryButton("Trust peer", OnTrustClicked);
        _trustButton.IsEnabled = false;
        _videoButton = PrimaryButton("Video", OnVideoClicked);
        _videoButton.IsEnabled = false;
        _muteButton = PrimaryButton("Mute", OnMuteClicked);
        _muteButton.IsEnabled = false;
        _createGroupButton = PrimaryButton("Create group", OnCreateGroupClicked);
        _createGroupButton.IsEnabled = false;
        _approveGroupButton = PrimaryButton("Approve group", OnApproveGroupClicked);
        _approveGroupButton.IsEnabled = false;
        _composer.KeyDown += OnComposerKeyDown;
        _groups.SelectionChanged += OnGroupSelectionChanged;
        _presence.NickSelected += OnPresenceSelected;

        _chatRail.Items =
        [
            new ChatRailItem(
                SpaceId.Default,
                "Default",
                ChannelId.Parse("#lobby")),
        ];
        _chatRail.ChannelSelected += OnChannelSelected;

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
        _session.DeviceFingerprintAvailable += fingerprint => Dispatcher.UIThread.Post(() =>
            _fingerprint.Text = $"Your device fingerprint: {fingerprint}");
        _session.PeerFingerprintAvailable += peer => Dispatcher.UIThread.Post(() => UpdatePeerTrust(peer));
        _session.PresenceChanged += presence => Dispatcher.UIThread.Post(() =>
            _presence.Presence = presence);
        _session.TypingChanged += typing => Dispatcher.UIThread.Post(() =>
        {
            var nicks = string.Join(", ", typing.Select(value => value.Nick));
            _status.Text = nicks.Length == 0 ? "Connected" : $"{nicks} typing…";
        });
        _session.ReceiptReceived += receipt => Dispatcher.UIThread.Post(() =>
            _status.Text = $"Read receipt from {receipt.Nick}");
        _session.GroupsChanged += groups => Dispatcher.UIThread.Post(() =>
        {
            _groups.ItemsSource = groups;
            if (_selectedGroup is { } selected)
                _groups.SelectedItem = groups.SingleOrDefault(group => group.GroupId == selected.GroupId);
        });
        _session.GroupHistoryReceived += messages => Dispatcher.UIThread.Post(() =>
        {
            foreach (var message in messages)
                AppendMessage(message);
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
                _fingerprint,
                _peerFingerprint,
            },
        });
        var nickRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _nickBox, _connectButton, _trustButton, _videoButton, _muteButton },
        };
        Grid.SetColumn(nickRow, 1);
        header.Children.Add(nickRow);

        var body = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("160,*,180"),
            Margin = new Thickness(12, 0, 12, 12),
            RowDefinitions = RowDefinitions.Parse("*"),
        };

        body.Children.Add(_chatRail);
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
            Child = _thread,
        });
        Grid.SetColumn(center, 1);
        center.Margin = new Thickness(8, 0);
        body.Children.Add(center);
        var groups = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                _groupNameBox,
                _groupMembersBox,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children = { _createGroupButton, _approveGroupButton },
                },
                _groups,
            },
        };
        var right = new StackPanel
        {
            Spacing = 8,
            Children = { Panel("Presence", _presence), Panel("Protected groups", groups) },
        };
        Grid.SetColumn(right, 2);
        body.Children.Add(right);

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

    async void OnChannelSelected(object? sender, ChatRailItem item)
    {
        if (!_session.IsConnected
            || string.Equals(_session.Channel, item.Channel.NormalizedName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            if (_video is not null)
            {
                await _video.DisposeAsync().ConfigureAwait(true);
                _video = null;
                _videoHost.IsVisible = false;
                _videoButton.Content = "Video";
                _muteButton.Content = "Mute";
                _muteButton.IsEnabled = false;
                RebuildVideoStrip();
            }

            await _session.SwitchChannelAsync(item.Channel.NormalizedName).ConfigureAwait(true);
            _selectedPeer = null;
            _composer.IsEnabled = false;
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
        }
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
            _composer.IsEnabled = false;
            _nickBox.IsEnabled = false;
            _connectButton.Content = "Connected";
            _videoButton.IsEnabled = true;
            _muteButton.IsEnabled = false;
            _createGroupButton.IsEnabled = true;
            _localSurface.Label = nick;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _connectButton.IsEnabled = true;
        }
    }

    async void OnPresenceSelected(object? sender, string nick)
    {
        _selectedGroup = null;
        _groups.SelectedItem = null;
        _selectedPeer = nick;
        _trustButton.IsEnabled = false;
        _composer.IsEnabled = false;
        if (string.IsNullOrWhiteSpace(_selectedPeer)
            || string.Equals(_selectedPeer, _session.Nick, StringComparison.OrdinalIgnoreCase)
            || !_session.IsConnected)
        {
            _selectedPeer = null;
            _peerFingerprint.Text = "Select a connected peer, then compare and confirm the displayed fingerprint.";
            return;
        }

        try
        {
            await _session.PreparePeerTrustAsync(_selectedPeer).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
        }
    }

    async void OnTrustClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedPeer))
            return;

        _trustButton.IsEnabled = false;
        try
        {
            await _session.ConfirmPeerTrustAsync(_selectedPeer).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
        }
    }

    void UpdatePeerTrust(PeerFingerprint peer)
    {
        if (!string.Equals(peer.Nick, _selectedPeer, StringComparison.OrdinalIgnoreCase))
            return;

        _peerFingerprint.Text = peer.IsTrusted
            ? $"Trusted {peer.Nick} device fingerprint: {peer.Fingerprint}"
            : $"Compare {peer.Nick} device fingerprint independently: {peer.Fingerprint}";
        _trustButton.IsEnabled = !peer.IsTrusted;
        if (_selectedGroup is not null)
            return;

        _composer.IsEnabled = peer.IsTrusted && _session.IsConnected;
        _composer.PlaceholderText = peer.IsTrusted
            ? $"Private message to {peer.Nick}"
            : "Confirm the selected peer before sending";
    }

    void OnGroupSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedGroup = _groups.SelectedItem as SecureTextGroup;
        _approveGroupButton.IsEnabled = _selectedGroup is { IsActive: false } && _session.IsConnected;
        _composer.IsEnabled = _selectedGroup is { IsActive: true } && _session.IsConnected;
        _composer.PlaceholderText = _selectedGroup switch
        {
            { IsActive: true } group => $"Encrypted group text to {group.Name}",
            { } group => $"Approve {group.Name} after verifying every member",
            _ => "Select and trust a peer",
        };
    }

    async void OnCreateGroupClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var name = _groupNameBox.Text?.Trim() ?? string.Empty;
        var members = (_groupMembersBox.Text ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        try
        {
            _createGroupButton.IsEnabled = false;
            await _session.CreateSecureTextGroupAsync(name, members).ConfigureAwait(true);
            _groupNameBox.Text = string.Empty;
            _groupMembersBox.Text = string.Empty;
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
        }
        finally
        {
            _createGroupButton.IsEnabled = _session.IsConnected;
        }
    }

    async void OnApproveGroupClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_selectedGroup is null)
            return;

        try
        {
            _approveGroupButton.IsEnabled = false;
            await _session.ApproveSecureTextGroupAsync(_selectedGroup.GroupId).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
            _approveGroupButton.IsEnabled = true;
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
                _muteButton.Content = "Mute";
                _muteButton.IsEnabled = false;
                RebuildVideoStrip();
                return;
            }

            _video = new MeshVideoController(_session, _localSurface);
            _video.SurfacesChanged += () => Dispatcher.UIThread.Post(RebuildVideoStrip);
            _video.AudioError += exception =>
                Dispatcher.UIThread.Post(() => _status.Text = $"Audio unavailable: {exception.Message}");
            await _video.StartAsync().ConfigureAwait(true);
            _videoHost.IsVisible = true;
            _videoButton.Content = "Video off";
            _muteButton.IsEnabled = true;
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
            _muteButton.Content = "Mute";
            _muteButton.IsEnabled = false;
        }
        finally
        {
            _videoButton.IsEnabled = _session.IsConnected;
        }
    }

    void OnMuteClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_video is null || !_video.IsVideoOn)
            return;

        _video.SetMuted(!_video.IsMuted);
        _muteButton.Content = _video.IsMuted ? "Unmute" : "Mute";
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
        var body = _composer.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(body) || !_session.IsConnected)
            return;
        if (_selectedGroup is null && string.IsNullOrWhiteSpace(_selectedPeer))
            return;

        _composer.Text = string.Empty;
        try
        {
            if (_selectedGroup is { IsActive: true } group)
                await _session.SayToSecureTextGroupAsync(group.GroupId, body).ConfigureAwait(true);
            else if (_selectedPeer is not null)
                await _session.SayAsync(_selectedPeer, body).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }

    void ReplaceHistory(IReadOnlyList<ChannelMessage> messages)
    {
        _messages.Clear();
        foreach (var message in messages)
            _messages.Add(ToChatMessage(message));
        _thread.Messages = _messages.ToArray();
    }

    void AppendMessage(ChannelMessage message)
    {
        _messages.Add(ToChatMessage(message));
        _thread.Messages = _messages.ToArray();
    }

    static ChatMessageDto ToChatMessage(ChannelMessage message)
    {
        var frame = message.Frame ?? ChatFrame.Create(
            message.Channel,
            Guid.NewGuid(),
            message.Nick,
            message.Channel,
            message.At);
        return new ChatMessageDto(frame, message.Body);
    }

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
