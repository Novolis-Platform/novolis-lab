using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CalypsoCad.Session;
using CalypsoCad.Viewport;
using CalypsoCad.Visibility;
using Novolis.Avalonia.GraphicalProfile;

namespace CalypsoCad.Explorer;

/// <summary>CAD explorer: camera / display / cut plane + hierarchical object visibility.</summary>
internal sealed class CalypsoExplorerWindow : Window
{
    private readonly CalypsoViewportController _controller;
    private readonly TextBlock _status;
    private readonly TextBlock _modeReadout;
    private readonly DispatcherTimer _statusTimer;
    private readonly List<ToggleButton> _viewToggles = [];
    private readonly List<ToggleButton> _displayToggles = [];
    private readonly List<ToggleButton> _deckToggles = [];
    private readonly Panel _cutPanel;
    private readonly CalypsoObjectBrowser _objectBrowser;
    private bool _syncingUi;

    public CalypsoExplorerWindow(CalypsoViewportController controller)
    {
        _controller = controller;
        Title = "Calypso CAD Explorer";
        Width = 1600;
        Height = 940;
        FontFamily = GraphicalProfile.BodyFont;

        ApplyHierarchyPreset(VisibilityPresets.PrimaryStructure, "primary", "PrimaryStructure · rings + girders + BH");

        GraphicalProfileBinding.Bind(this, BackgroundProperty, GraphicalProfile.BackgroundResourceKey);

        var viewport = new CalypsoSilkViewportControl
        {
            Controller = controller,
            FrameWidth = Novolis.Silk.Capture.SilkCaptureSize.UhdWidth,
            FrameHeight = Novolis.Silk.Capture.SilkCaptureSize.UhdHeight,
            [DockPanel.DockProperty] = Dock.Right,
        };

        _modeReadout = new TextBlock
        {
            FontFamily = GraphicalProfile.MonoFont,
            FontSize = 12,
            Margin = new Thickness(8, 6, 8, 2),
            TextWrapping = TextWrapping.Wrap,
        };
        GraphicalProfileBinding.Bind(_modeReadout, TextBlock.ForegroundProperty, GraphicalProfile.TextResourceKey);

        _status = new TextBlock
        {
            Margin = new Thickness(8, 4),
            FontFamily = GraphicalProfile.MonoFont,
            FontSize = 11,
        };
        GraphicalProfileBinding.Bind(_status, TextBlock.ForegroundProperty, GraphicalProfile.MutedResourceKey);

        _cutPanel = new StackPanel { Spacing = 4 };
        _objectBrowser = new CalypsoObjectBrowser(controller);
        var chrome = BuildChrome();
        chrome[DockPanel.DockProperty] = Dock.Left;

        var statusBar = new Border
        {
            Child = _status,
            [DockPanel.DockProperty] = Dock.Bottom,
            Padding = new Thickness(4, 2),
        };
        GraphicalProfileBinding.Bind(statusBar, Border.BackgroundProperty, GraphicalProfile.SurfaceResourceKey);

        Content = new DockPanel
        {
            LastChildFill = true,
            Children = { statusBar, chrome, viewport },
        };

        _controller.Session.Visibility.Changed += () =>
            Dispatcher.UIThread.Post(() => _objectBrowser.RebuildTree());

        _statusTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, (_, _) => SyncUiFromSession());
        _statusTimer.Start();
        Closed += (_, _) => _statusTimer.Stop();
        SyncUiFromSession();
    }

    private void ApplyHierarchyPreset(Func<VisibilitySnapshot> build, string id, string status)
    {
        _controller.SetView(CalypsoViewMode.Orbit);
        _controller.SetWire(CalypsoWireMeshMode.None);
        _controller.Session.Visibility.ApplySnapshot(build(), asPreset: id);
        if (string.Equals(id, "pressure", StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, "cargo", StringComparison.OrdinalIgnoreCase))
            _controller.Session.Visibility.SetLocal("ref-oml-edge-guide", LocalVisibility.Visible, recordUndo: false);
        _controller.Session.DeckFilter = null;
        _controller.Session.Visibility.ApplyDeckFocus(null, recordUndo: false);
        _controller.Session.StatusText = $"preset:{status}";
        _objectBrowser.RebuildTree();
    }

    private Control BuildChrome()
    {
        var panel = new StackPanel
        {
            Width = 280,
            Margin = new Thickness(8),
            Spacing = 4,
        };
        GraphicalProfileBinding.Bind(panel, BackgroundProperty, GraphicalProfile.SurfaceResourceKey);

        panel.Children.Add(_modeReadout);

        panel.Children.Add(Header("Camera"));
        panel.Children.Add(RadioRow(_viewToggles,
            ("Plan", () => _controller.SetView(CalypsoViewMode.Plan)),
            ("Orbit", () => _controller.SetView(CalypsoViewMode.Orbit)),
            ("Interior", () => _controller.SetInterior())));

        panel.Children.Add(Header("Display"));
        panel.Children.Add(RadioRow(_displayToggles,
            ("Shaded", () => _controller.SetWire(CalypsoWireMeshMode.None)),
            ("Wire", () => _controller.SetWire(CalypsoWireMeshMode.Wire)),
            ("Cutaway", () => _controller.SetWire(CalypsoWireMeshMode.CutawayPartial))));
        panel.Children.Add(Hint("Shaded / Wire / Cutaway are presentation modes — not object visibility."));

        panel.Children.Add(Header("Deck focus"));
        panel.Children.Add(RadioRow(_deckToggles,
            ("−1", () => ApplyDeck(-1)),
            ("0", () => ApplyDeck(0)),
            ("+1", () => ApplyDeck(1)),
            ("All", () => ApplyDeck(null))));
        panel.Children.Add(Hint("Overrides Deck −1/0/+1 nodes in the hierarchy. Full-height structure stays."));

        panel.Children.Add(Header("Visibility presets"));
        panel.Children.Add(Btn("1 Primary structure", () => ApplyHierarchyPreset(
            VisibilityPresets.PrimaryStructure, "primary", "PrimaryStructure · rings + girders + BH")));
        panel.Children.Add(Btn("2 Primary + decks", () => ApplyHierarchyPreset(
            VisibilityPresets.PrimaryPlusDecks, "primary-decks", "PrimaryPlusDecks · + sandwich plates")));
        panel.Children.Add(Btn("3 Pressure structure", () => ApplyHierarchyPreset(
            VisibilityPresets.PressureStructure, "pressure", "PressureStructure · shell + BH + openings")));
        panel.Children.Add(Btn("4 Complete arrangement", () => ApplyHierarchyPreset(
            VisibilityPresets.Complete, "complete", "CompleteArrangement · full ship")));
        panel.Children.Add(Btn("HOLD cargo", () => ApplyHierarchyPreset(
            VisibilityPresets.CargoHold, "cargo", "HOLD cargo · Shaded")));

        panel.Children.Add(_objectBrowser);
        panel.Children.Add(Btn("Refresh object tree", () => _objectBrowser.RebuildTree()));

        panel.Children.Add(Header("Section cut (Cutaway only)"));
        _cutPanel.Children.Add(Row(
            Btn("Longitudinal", () => { _controller.SetCutLongitudinal(true); _controller.Session.CutPlaneUserDriven = true; }),
            Btn("Beam", () => { _controller.SetCutLongitudinal(false); _controller.Session.CutPlaneUserDriven = true; })));
        _cutPanel.Children.Add(Row(
            Btn("Offset −", () => { _controller.NudgeCut(-1f); _controller.Session.CutPlaneUserDriven = true; }),
            Btn("Offset +", () => { _controller.NudgeCut(1f); _controller.Session.CutPlaneUserDriven = true; })));
        _cutPanel.Children.Add(Hint("Disabled unless Display = Cutaway."));
        panel.Children.Add(_cutPanel);

        panel.Children.Add(Header("Session"));
        panel.Children.Add(Btn("Regenerate model", () =>
        {
            _controller.Regenerate();
            _objectBrowser.RebuildTree();
        }));
        panel.Children.Add(Btn("Export blueprints", () =>
        {
            try
            {
                CalypsoCad.Blueprints.InternalsBlueprintEmitter.Emit();
                CalypsoCad.Blueprints.StructureDetailBlueprintEmitter.Emit();
                _controller.Session.StatusText = "blueprints written";
            }
            catch (Exception ex)
            {
                _controller.Session.StatusText = $"bp:{ex.Message}";
            }
        }));

        var scroll = new ScrollViewer
        {
            Content = panel,
            Width = 296,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var border = new Border
        {
            Child = scroll,
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
        };
        GraphicalProfileBinding.Bind(border, Border.BackgroundProperty, GraphicalProfile.SurfaceResourceKey);
        GraphicalProfileBinding.Bind(border, Border.BorderBrushProperty, GraphicalProfile.BorderResourceKey);
        return border;
    }

    private void ApplyDeck(int? deck)
    {
        _controller.SetDeckFilter(deck);
        _controller.Session.Visibility.ApplyDeckFocus(deck);
        _controller.Session.StatusText = deck is null ? "deck:all" : $"deck:{deck.Value}";
        _objectBrowser.RebuildTree();
    }

    private void SyncUiFromSession()
    {
        _syncingUi = true;
        try
        {
            var s = _controller.Session;
            var display = s.WireMeshMode switch
            {
                CalypsoWireMeshMode.None => "Shaded",
                CalypsoWireMeshMode.Wire => "Wire",
                _ => "Cutaway",
            };
            var deck = s.DeckFilter switch
            {
                -1 => "DK−1",
                0 => "DK0",
                1 => "DK+1",
                _ => "all decks",
            };
            var iso = s.Visibility.IsolationActive ? " · ISOLATION" : "";
            var preset = s.Visibility.ActivePresetLabel is { } p ? $"\nVis: {p}" : "";
            _modeReadout.Text =
                $"Camera: {s.ViewMode}\nDisplay: {display}\nDeck: {deck}{iso}{preset}\n" +
                $"Cut: {(s.WireMeshMode == CalypsoWireMeshMode.CutawayPartial ? (s.CutPlaneLongitudinal ? "L" : "B") + $" @ {s.CutPlaneOffset:0.#} m" : "off")}";
            _status.Text = s.StatusText;

            SetRadio(_viewToggles, s.ViewMode switch
            {
                CalypsoViewMode.Plan => 0,
                CalypsoViewMode.Interior => 2,
                _ => 1,
            });
            SetRadio(_displayToggles, s.WireMeshMode switch
            {
                CalypsoWireMeshMode.None => 0,
                CalypsoWireMeshMode.Wire => 1,
                _ => 2,
            });
            SetRadio(_deckToggles, s.DeckFilter switch
            {
                -1 => 0,
                0 => 1,
                1 => 2,
                _ => 3,
            });

            var cutOn = s.WireMeshMode == CalypsoWireMeshMode.CutawayPartial;
            _cutPanel.IsEnabled = cutOn;
            _cutPanel.Opacity = cutOn ? 1 : 0.45;
        }
        finally
        {
            _syncingUi = false;
        }
    }

    private StackPanel RadioRow(List<ToggleButton> store, params (string Label, Action Act)[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        for (var i = 0; i < items.Length; i++)
        {
            var (label, act) = items[i];
            var tb = new ToggleButton
            {
                Content = label,
                Padding = new Thickness(8, 4),
                FontSize = 11,
                MinWidth = 56,
            };
            tb.IsCheckedChanged += (_, _) =>
            {
                if (_syncingUi || tb.IsChecked != true)
                    return;
                foreach (var other in store)
                {
                    if (!ReferenceEquals(other, tb))
                        other.IsChecked = false;
                }

                act();
            };
            store.Add(tb);
            row.Children.Add(tb);
        }

        return row;
    }

    private static void SetRadio(List<ToggleButton> store, int index)
    {
        for (var i = 0; i < store.Count; i++)
            store[i].IsChecked = i == index;
    }

    private static StackPanel Row(params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var c in children)
            row.Children.Add(c);
        return row;
    }

    private static TextBlock Header(string text)
    {
        var t = new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 8, 0, 2),
            FontSize = 12,
        };
        GraphicalProfileBinding.Bind(t, TextBlock.ForegroundProperty, GraphicalProfile.TextResourceKey);
        return t;
    }

    private static TextBlock Hint(string text)
    {
        var t = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Margin = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        GraphicalProfileBinding.Bind(t, TextBlock.ForegroundProperty, GraphicalProfile.MutedResourceKey);
        return t;
    }

    private static Button Btn(string text, Action act)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(8, 4),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        b.Click += (_, _) => act();
        return b;
    }
}
