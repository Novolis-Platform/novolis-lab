using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FriendLab.Core;
using FriendLab.Ui;

namespace FriendLab.Windows;

internal sealed class UserWindow : Window
{
    readonly FriendHub _hub;
    readonly FriendProfile _live;
    readonly NeighborhoodMap _map = new();
    readonly WrapPanel _interestWrap = new() { Orientation = Orientation.Horizontal };
    readonly Slider _radiusSlider;
    readonly TextBlock _radiusLabel = new() { FontFamily = FriendPalette.Body, Foreground = FriendPalette.InkMutedBrush };
    readonly TextBlock _status = new()
    {
        FontFamily = FriendPalette.Body,
        FontSize = 13,
        Foreground = FriendPalette.InkMutedBrush,
        TextWrapping = TextWrapping.Wrap,
    };
    readonly StackPanel _matchHost = new() { Spacing = 10 };
    readonly Dictionary<string, ToggleButton> _interestButtons = new(StringComparer.OrdinalIgnoreCase);
    readonly IDisposable _sub;
    bool _suppress;

    public string ProfileId => _live.Id;

    public UserWindow(FriendHub hub, FriendProfile live)
    {
        _hub = hub;
        _live = live;

        Title = $"{live.DisplayName} — Find a Friend";
        Width = 520;
        Height = 760;
        MinWidth = 420;
        MinHeight = 560;
        Background = FriendPalette.MistSoftBrush;

        _radiusSlider = new Slider
        {
            Minimum = 0.5,
            Maximum = 25,
            Value = live.RadiusKm,
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _radiusSlider.PropertyChanged += (_, e) =>
        {
            if (_suppress || e.Property != RangeBase.ValueProperty)
                return;
            _live.RadiusKm = Math.Round(_radiusSlider.Value, 1);
            Push();
        };

        BuildInterestChips();
        _map.EditableId = live.Id;
        _map.HighlightId = live.Id;
        _map.LocationDragged += OnMapDrag;

        Content = BuildChrome();
        _sub = hub.Subscribe(() => Dispatcher.UIThread.Post(RefreshFromHub));
        RefreshFromHub();
    }

    protected override void OnClosed(EventArgs e)
    {
        _sub.Dispose();
        base.OnClosed(e);
    }

    Control BuildChrome()
    {
        var accent = Color.Parse(_live.AccentHex);
        var header = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                [
                    new GradientStop(FriendPalette.PineDeep, 0),
                    new GradientStop(FriendPalette.Pine, 0.65),
                    new GradientStop(accent, 1),
                ],
            },
            Padding = new Thickness(20, 18),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Find a Friend",
                        FontFamily = FriendPalette.Display,
                        FontSize = 28,
                        FontWeight = FontWeight.Bold,
                        Foreground = FriendPalette.MistSoftBrush,
                    },
                    new TextBlock
                    {
                        Text = $"signed in as {_live.DisplayName}",
                        FontFamily = FriendPalette.Body,
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Color.FromArgb(220, 238, 244, 240)),
                    },
                },
            },
        };

        var interestSection = Section(
            "Your five interests",
            "Pick exactly five. Matches need three or more in common.",
            _interestWrap);

        var radiusRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _radiusSlider, _radiusLabel },
        };

        var radiusSection = Section("Search radius", "How far you are willing to travel for a first meetup.", radiusRow);

        var mapBorder = new Border
        {
            Height = 200,
            CornerRadius = new CornerRadius(8),
            BorderBrush = FriendPalette.EdgeBrush,
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = _map,
        };
        var mapHint = new TextBlock
        {
            Text = "Drag your pin to move. Ring shows your radius.",
            FontFamily = FriendPalette.Body,
            FontSize = 12,
            Foreground = FriendPalette.InkMutedBrush,
            Margin = new Thickness(0, 6, 0, 0),
        };

        var matchSection = Section("Nearby matches", "Live against other open user windows + the shared directory.", _matchHost);

        var body = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 16,
            Children =
            {
                interestSection,
                radiusSection,
                new StackPanel { Children = { mapBorder, mapHint } },
                _status,
                matchSection,
            },
        };

        return new ScrollViewer
        {
            Content = new DockPanel
            {
                LastChildFill = true,
                Children =
                {
                    header.WithDock(Dock.Top),
                    body,
                },
            },
        };
    }

    void BuildInterestChips()
    {
        _interestWrap.Children.Clear();
        _interestButtons.Clear();
        foreach (var interest in InterestCatalog.All)
        {
            var btn = new ToggleButton
            {
                Content = interest,
                Padding = new Thickness(10, 5),
                Margin = new Thickness(0, 0, 6, 6),
                FontFamily = FriendPalette.Body,
                FontSize = 12,
                CornerRadius = new CornerRadius(14),
                IsChecked = _live.Interests.Contains(interest),
            };
            btn.IsCheckedChanged += (_, _) => OnInterestToggled(interest, btn);
            _interestButtons[interest] = btn;
            _interestWrap.Children.Add(btn);
        }
    }

    void OnInterestToggled(string interest, ToggleButton btn)
    {
        if (_suppress)
            return;

        if (btn.IsChecked == true)
        {
            if (_live.Interests.Count >= InterestCatalog.RequiredPicks && !_live.Interests.Contains(interest))
            {
                _suppress = true;
                btn.IsChecked = false;
                _suppress = false;
                _status.Text = "Already at five interests — deselect one first.";
                return;
            }

            _live.Interests.Add(interest);
        }
        else
        {
            _live.Interests.Remove(interest);
        }

        Push();
    }

    void OnMapDrag(string id, double lat, double lon)
    {
        if (id != _live.Id)
            return;
        _live.Latitude = lat;
        _live.Longitude = lon;
        Push();
    }

    void Push()
    {
        _hub.PublishChanged();
        RefreshFromHub();
    }

    void RefreshFromHub()
    {
        _suppress = true;
        try
        {
            _radiusSlider.Value = _live.RadiusKm;
            _radiusLabel.Text = $"{_live.RadiusKm:0.0} km";

            foreach (var (interest, btn) in _interestButtons)
                btn.IsChecked = _live.Interests.Contains(interest);

            var everyone = _hub.Snapshot();
            // Prefer live objects for match so we see edits from other windows via hub snapshots
            // Snapshot clones are fine for matching.
            var viewer = everyone.FirstOrDefault(p => p.Id == _live.Id) ?? _live.CloneSnapshot();
            // Sync map from live positions in hub
            _map.Profiles = everyone;
            _map.HighlightId = _live.Id;

            if (viewer.Interests.Count != InterestCatalog.RequiredPicks)
            {
                _status.Text = $"Select {InterestCatalog.RequiredPicks - viewer.Interests.Count} more interest(s) to start matching.";
                _matchHost.Children.Clear();
                _matchHost.Children.Add(Muted("No matches yet — finish picking five interests."));
                return;
            }

            var matches = MatchEngine.FindMatches(viewer, everyone);
            _status.Text = matches.Count == 0
                ? "No one nearby shares at least three interests yet. Nudge radius, move pin, or open other users."
                : $"{matches.Count} match(es) within {viewer.RadiusKm:0.0} km (≥{InterestCatalog.MinOverlap} shared interests).";

            _matchHost.Children.Clear();
            if (matches.Count == 0)
            {
                _matchHost.Children.Add(Muted("Empty — try Blair, Drew, or Fran near Alex."));
                return;
            }

            foreach (var match in matches)
                _matchHost.Children.Add(MatchCard(match));
        }
        finally
        {
            _suppress = false;
        }
    }

    static Control MatchCard(MatchResult match)
    {
        var accent = Color.Parse(match.Candidate.AccentHex);
        var mutual = match.WithinTheirRadius ? "mutual radius" : "you see them (not mutual yet)";
        return new Border
        {
            Background = FriendPalette.PanelBrush,
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 10),
            CornerRadius = new CornerRadius(6),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = match.Candidate.DisplayName,
                        FontFamily = FriendPalette.Display,
                        FontSize = 18,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = FriendPalette.InkBrush,
                    },
                    new TextBlock
                    {
                        Text = $"{match.DistanceKm:0.00} km · {match.SharedInterests.Count}/5 shared · {mutual}",
                        FontFamily = FriendPalette.Body,
                        FontSize = 12,
                        Foreground = FriendPalette.InkMutedBrush,
                    },
                    new TextBlock
                    {
                        Text = string.Join(" · ", match.SharedInterests),
                        FontFamily = FriendPalette.Body,
                        FontSize = 13,
                        Foreground = FriendPalette.PineBrush,
                    },
                    new TextBlock
                    {
                        Text = match.SuggestedActivity,
                        FontFamily = FriendPalette.Body,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = FriendPalette.InkBrush,
                        Margin = new Thickness(0, 4, 0, 0),
                    },
                },
            },
        };
    }

    static Control Section(string title, string subtitle, Control body) =>
        new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontFamily = FriendPalette.Display,
                    FontSize = 18,
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
                body,
            },
        };

    static TextBlock Muted(string text) => new()
    {
        Text = text,
        FontFamily = FriendPalette.Body,
        FontSize = 13,
        Foreground = FriendPalette.InkMutedBrush,
        TextWrapping = TextWrapping.Wrap,
    };
}
