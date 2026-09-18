using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FriendLab.Core;
using FriendLab.Ui;

namespace FriendLab.Windows;

internal sealed class ControlWindow : Window
{
    readonly FriendHub _hub;
    readonly Dictionary<string, UserWindow> _openUsers = new(StringComparer.Ordinal);
    readonly StackPanel _roster = new() { Spacing = 8 };
    readonly StackPanel _matrix = new() { Spacing = 6 };
    readonly NeighborhoodMap _map = new();
    readonly TextBlock _blurb = new()
    {
        FontFamily = FriendPalette.Body,
        FontSize = 13,
        Foreground = FriendPalette.MistSoftBrush,
        TextWrapping = TextWrapping.Wrap,
        Text = "Meet people where three of five interests overlap, nearby. " +
               "Not dating. Not a feed. Each window below is a separate app user sharing one in-memory directory.",
    };
    readonly IDisposable _sub;

    public ControlWindow(FriendHub hub)
    {
        _hub = hub;
        Title = "FriendLab — Find a Friend control";
        Width = 980;
        Height = 720;
        MinWidth = 800;
        MinHeight = 560;
        Background = FriendPalette.PineDeepBrush;

        Content = BuildChrome();
        _sub = hub.Subscribe(() => Dispatcher.UIThread.Post(Refresh));
        Refresh();
        Opened += (_, _) =>
        {
            // Immediate multi-user control: three phones side by side
            OpenOrFocus("alex");
            OpenOrFocus("blair");
            OpenOrFocus("drew");
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        _sub.Dispose();
        foreach (var window in _openUsers.Values.ToArray())
            window.Close();
        base.OnClosed(e);
    }

    Control BuildChrome()
    {
        var brand = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Find a Friend",
                    FontFamily = FriendPalette.Display,
                    FontSize = 42,
                    FontWeight = FontWeight.Bold,
                    Foreground = FriendPalette.MistSoftBrush,
                },
                new TextBlock
                {
                    Text = "FriendLab dogfood · Harbor District scenario",
                    FontFamily = FriendPalette.Body,
                    FontSize = 14,
                    Foreground = FriendPalette.SignalBrush,
                },
                _blurb,
            },
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 14, 0, 0),
            Children =
            {
                PrimaryButton("Open all users", OpenAll),
                GhostButton("Reset demo seed", ResetSeed),
                GhostButton("Close user windows", CloseUsers),
            },
        };

        var header = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                [
                    new GradientStop(FriendPalette.PineDeep, 0),
                    new GradientStop(FriendPalette.Pine, 0.7),
                    new GradientStop(Color.Parse("#2a5243"), 1),
                ],
            },
            Padding = new Thickness(28, 24, 28, 20),
            Child = new StackPanel { Children = { brand, actions } },
        };

        _map.Height = 280;
        var mapPane = PanelCard(
            "Neighborhood",
            "Pins update live. Open a user window and drag their pin or change interests.",
            new Border
            {
                Height = 280,
                CornerRadius = new CornerRadius(8),
                BorderBrush = FriendPalette.EdgeBrush,
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Child = _map,
            });

        var rosterPane = PanelCard("App users", "One window = one phone in your hand.", _roster);
        var matrixPane = PanelCard("Match matrix", "Who currently qualifies for whom.", _matrix);

        var columns = new Grid
        {
            Margin = new Thickness(18),
            ColumnDefinitions = new ColumnDefinitions("1.1*, 0.9*"),
            ColumnSpacing = 14,
            RowDefinitions = new RowDefinitions("Auto, *"),
            RowSpacing = 14,
        };
        Grid.SetColumn(mapPane, 0);
        Grid.SetRow(mapPane, 0);
        Grid.SetColumnSpan(mapPane, 2);
        Grid.SetColumn(rosterPane, 0);
        Grid.SetRow(rosterPane, 1);
        Grid.SetColumn(matrixPane, 1);
        Grid.SetRow(matrixPane, 1);
        columns.Children.Add(mapPane);
        columns.Children.Add(rosterPane);
        columns.Children.Add(matrixPane);

        return new DockPanel
        {
            LastChildFill = true,
            Children =
            {
                header.WithDock(Dock.Top),
                columns,
            },
        };
    }

    void Refresh()
    {
        var everyone = _hub.Snapshot();
        _map.Profiles = everyone;

        _roster.Children.Clear();
        foreach (var profile in everyone)
            _roster.Children.Add(RosterRow(profile));

        _matrix.Children.Clear();
        foreach (var viewer in everyone)
        {
            var matches = MatchEngine.FindMatches(viewer, everyone);
            var line = matches.Count == 0
                ? $"{viewer.DisplayName}: —"
                : $"{viewer.DisplayName}: {string.Join(", ", matches.Select(m => $"{m.Candidate.DisplayName.Split(' ')[0]} ({m.SharedInterests.Count}/5, {m.DistanceKm:0.0}km)"))}";
            _matrix.Children.Add(new TextBlock
            {
                Text = line,
                FontFamily = FriendPalette.Body,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = FriendPalette.InkBrush,
            });
        }
    }

    Control RosterRow(FriendProfile profile)
    {
        var open = _openUsers.ContainsKey(profile.Id);
        var accent = Color.Parse(profile.AccentHex);
        var openBtn = PrimaryButton(open ? "Focus" : "Open window", () => OpenOrFocus(profile.Id));
        openBtn.Width = 120;

        return new Border
        {
            Background = FriendPalette.PanelBrush,
            BorderBrush = FriendPalette.EdgeBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto"),
                ColumnSpacing = 10,
                Children =
                {
                    new Border
                    {
                        Width = 12,
                        Height = 12,
                        CornerRadius = new CornerRadius(6),
                        Background = new SolidColorBrush(accent),
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    new StackPanel
                    {
                        [Grid.ColumnProperty] = 1,
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = profile.DisplayName,
                                FontFamily = FriendPalette.Display,
                                FontSize = 16,
                                Foreground = FriendPalette.InkBrush,
                            },
                            new TextBlock
                            {
                                Text = $"{string.Join(", ", profile.Interests.OrderBy(i => i))} · r={profile.RadiusKm:0.0}km",
                                FontFamily = FriendPalette.Body,
                                FontSize = 11,
                                Foreground = FriendPalette.InkMutedBrush,
                                TextWrapping = TextWrapping.Wrap,
                            },
                        },
                    },
                    new Border { [Grid.ColumnProperty] = 2, Child = openBtn, VerticalAlignment = VerticalAlignment.Center },
                },
            },
        };
    }

    void OpenAll()
    {
        foreach (var profile in _hub.Snapshot())
            OpenOrFocus(profile.Id);
    }

    void OpenOrFocus(string id)
    {
        if (_openUsers.TryGetValue(id, out var existing))
        {
            existing.Activate();
            return;
        }

        var live = _hub.GetLive(id);
        if (live is null)
            return;

        var window = new UserWindow(_hub, live);
        window.Closed += (_, _) => _openUsers.Remove(id);
        _openUsers[id] = window;

        // Cascade so windows don't stack perfectly
        var n = _openUsers.Count;
        window.Position = new PixelPoint(80 + n * 36, 60 + n * 28);
        window.Show(this);
        Refresh();
    }

    void CloseUsers()
    {
        foreach (var window in _openUsers.Values.ToArray())
            window.Close();
        _openUsers.Clear();
        Refresh();
    }

    void ResetSeed()
    {
        CloseUsers();
        _hub.ReplaceAll(DemoSeed.CreateHarborDistrict());
    }

    static Border PanelCard(string title, string subtitle, Control body) =>
        new()
        {
            Background = FriendPalette.MistSoftBrush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Child = new DockPanel
            {
                LastChildFill = true,
                Children =
                {
                    new StackPanel
                    {
                        [DockPanel.DockProperty] = Dock.Top,
                        Spacing = 2,
                        Margin = new Thickness(0, 0, 0, 10),
                        Children =
                        {
                            new TextBlock
                            {
                                Text = title,
                                FontFamily = FriendPalette.Display,
                                FontSize = 20,
                                Foreground = FriendPalette.PineBrush,
                            },
                            new TextBlock
                            {
                                Text = subtitle,
                                FontFamily = FriendPalette.Body,
                                FontSize = 12,
                                Foreground = FriendPalette.InkMutedBrush,
                                TextWrapping = TextWrapping.Wrap,
                            },
                        },
                    },
                    new ScrollViewer { Content = body },
                },
            },
        };

    static Button PrimaryButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Content = text,
            FontFamily = FriendPalette.Body,
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(14, 8),
            Background = FriendPalette.SignalBrush,
            Foreground = FriendPalette.PineDeepBrush,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(6),
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    static Button GhostButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Content = text,
            FontFamily = FriendPalette.Body,
            Padding = new Thickness(14, 8),
            Background = Brushes.Transparent,
            Foreground = FriendPalette.MistSoftBrush,
            BorderBrush = FriendPalette.EdgeBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }
}
