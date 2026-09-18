using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ChannelLab.Services;
using ChannelLab.Ui;

namespace ChannelLab.Windows;

internal sealed class ControlWindow : Window
{
    readonly HostProcessGuard _host;
    readonly List<PeerWindow> _peers = [];
    readonly TextBlock _hostStatus = new()
    {
        FontFamily = ChannelPalette.Body,
        FontSize = 13,
        Foreground = ChannelPalette.InkMutedBrush,
        Text = "Host: checking…",
    };
    int _peerSerial = 1;

    public ControlWindow(HostProcessGuard host)
    {
        _host = host;
        Title = "ChannelLab";
        Width = 640;
        Height = 420;
        MinWidth = 480;
        MinHeight = 320;
        Background = ChannelPalette.NavyDeepBrush;
        Content = BuildChrome();
        Opened += async (_, _) => await BootstrapAsync().ConfigureAwait(true);
    }

    Control BuildChrome()
    {
        var brand = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = "ChannelLab",
                    FontFamily = ChannelPalette.Display,
                    FontSize = 40,
                    FontWeight = FontWeight.Bold,
                    Foreground = ChannelPalette.MistSoftBrush,
                },
                new TextBlock
                {
                    Text = "IRC · Avalonia · #lobby",
                    FontFamily = ChannelPalette.Body,
                    FontSize = 14,
                    Foreground = ChannelPalette.CopperBrush,
                },
                new TextBlock
                {
                    Text = "Open two peer windows, connect with different nicks, and prove fan-out. " +
                           "Toggle Video for Avalonia mesh tiles (max 4) — chat keeps working if capture fails.",
                    FontFamily = ChannelPalette.Body,
                    FontSize = 13,
                    Foreground = ChannelPalette.InkMutedBrush,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 560,
                },
                _hostStatus,
            },
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 18, 0, 0),
            Children =
            {
                PrimaryButton("Ensure host", async () => await EnsureHostAsync()),
                PrimaryButton("Open peer", () => OpenPeer()),
                GhostButton("Open alice + bob", OpenDemoPair),
                GhostButton("Close peers", ClosePeers),
            },
        };

        return new Border
        {
            Padding = new Thickness(28),
            Child = new StackPanel
            {
                Spacing = 8,
                Children = { brand, actions },
            },
        };
    }

    async Task BootstrapAsync()
    {
        await EnsureHostAsync().ConfigureAwait(true);
        OpenPeer("alice");
        OpenPeer("bob");
    }

    async Task EnsureHostAsync()
    {
        _hostStatus.Text = "Host: starting…";
        var ok = await _host.EnsureRunningAsync().ConfigureAwait(true);
        _hostStatus.Text = ok
            ? $"Host: ok at {HostEndpoints.BaseUri}"
            : "Host: failed — run ChannelHost manually (see README).";
        _hostStatus.Foreground = ok ? ChannelPalette.TealBrush : ChannelPalette.CopperBrush;
    }

    void OpenDemoPair()
    {
        OpenPeer("alice");
        OpenPeer("bob");
    }

    void OpenPeer(string? nick = null)
    {
        nick ??= $"guest{_peerSerial++}";
        var peer = new PeerWindow(nick);
        _peers.Add(peer);
        peer.Closed += (_, _) => _peers.Remove(peer);
        peer.Show(this);
    }

    void ClosePeers()
    {
        foreach (var peer in _peers.ToArray())
            peer.Close();
        _peers.Clear();
    }

    static Button PrimaryButton(string label, Action handler)
    {
        var button = new Button
        {
            Content = label,
            Background = ChannelPalette.TealBrush,
            Foreground = ChannelPalette.MistSoftBrush,
            Padding = new Thickness(14, 8),
            FontFamily = ChannelPalette.Body,
        };
        button.Click += (_, _) => handler();
        return button;
    }

    static Button PrimaryButton(string label, Func<Task> handler)
    {
        var button = new Button
        {
            Content = label,
            Background = ChannelPalette.TealBrush,
            Foreground = ChannelPalette.MistSoftBrush,
            Padding = new Thickness(14, 8),
            FontFamily = ChannelPalette.Body,
        };
        button.Click += async (_, _) =>
        {
            try { await handler().ConfigureAwait(true); }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() => { /* swallow to status via caller */ _ = ex; });
            }
        };
        return button;
    }

    static Button GhostButton(string label, Action handler)
    {
        var button = new Button
        {
            Content = label,
            Background = ChannelPalette.PanelLiftBrush,
            Foreground = ChannelPalette.MistBrush,
            Padding = new Thickness(14, 8),
            FontFamily = ChannelPalette.Body,
        };
        button.Click += (_, _) => handler();
        return button;
    }

    protected override async void OnClosed(EventArgs e)
    {
        ClosePeers();
        await _host.DisposeAsync().ConfigureAwait(true);
        base.OnClosed(e);
    }
}
