using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CalypsoCad.Session;
using CalypsoCad.Viewport;
using Novolis.Avalonia.GraphicalProfile;

namespace CalypsoCad.Explorer;

/// <summary>CAD-style explorer: radio groups for mode, checkboxes for layers, live state.</summary>
internal sealed class CalypsoExplorerWindow : Window
{
    private readonly CalypsoViewportController _controller;
    private readonly TextBlock _status;
    private readonly TextBlock _modeReadout;
    private readonly DispatcherTimer _statusTimer;
    private readonly Dictionary<CalypsoVisualLayers, CheckBox> _layerChecks = new();
    private readonly List<ToggleButton> _viewToggles = [];
    private readonly List<ToggleButton> _displayToggles = [];
    private readonly List<ToggleButton> _deckToggles = [];
    private readonly Panel _cutPanel;
    private readonly CalypsoObjectBrowser _objectBrowser;
    private CheckBox? _omlGuideCheck;
    private bool _syncingUi;

    public CalypsoExplorerWindow(CalypsoViewportController controller)
    {
        _controller = controller;
        Title = "Calypso CAD Explorer";
        Width = 1600;
        Height = 940;
        FontFamily = GraphicalProfile.BodyFont;

        ApplyStagePreset(CalypsoVisualLayers.PrimaryStructure, "PrimaryStructure · rings + girders + BH");

        GraphicalProfileBinding.Bind(this, BackgroundProperty, GraphicalProfile.BackgroundResourceKey);

        var viewport = new CalypsoSilkViewportControl
        {
            Controller = controller,
            FrameWidth = 1024,
            FrameHeight = 640,
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

        _statusTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, (_, _) => SyncUiFromSession());
        _statusTimer.Start();
        Closed += (_, _) => _statusTimer.Stop();
        SyncUiFromSession();
    }

    private void ApplyStagePreset(CalypsoVisualLayers layers, string status)
    {
        _controller.SetView(CalypsoViewMode.Orbit);
        _controller.SetWire(CalypsoWireMeshMode.None);
        _controller.Session.VisualLayers = layers;
        _controller.Session.ShowOmlGuide = layers == CalypsoVisualLayers.PressureStructure
                                          || layers == CalypsoVisualLayers.HoldCargo;
        _controller.Session.DeckFilter = null;
        _controller.Session.StatusText = $"preset:{status}";
    }

    private void ApplyPresetHoldCargo()
    {
        ApplyStagePreset(CalypsoVisualLayers.HoldCargo, "HOLD cargo · Shaded");
        _controller.Session.ShowOmlGuide = true;
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
        panel.Children.Add(Hint("Shaded = full solids (no cut). Cutaway enables the section plane."));

        panel.Children.Add(Header("Deck filter (deck-scoped only)"));
        panel.Children.Add(RadioRow(_deckToggles,
            ("−1", () => _controller.SetDeckFilter(-1)),
            ("0", () => _controller.SetDeckFilter(0)),
            ("+1", () => _controller.SetDeckFilter(1)),
            ("All", () => _controller.SetDeckFilter(null))));
        panel.Children.Add(Hint("Does not hide Ship layer (hull / frames / BHs / girders)."));

        panel.Children.Add(Header("Presets"));
        panel.Children.Add(Btn("1 Primary structure", () => ApplyStagePreset(
            CalypsoVisualLayers.PrimaryStructure, "PrimaryStructure · rings + girders + BH")));
        panel.Children.Add(Btn("2 Primary + decks", () => ApplyStagePreset(
            CalypsoVisualLayers.PrimaryPlusDecks, "PrimaryPlusDecks · + sandwich plates")));
        panel.Children.Add(Btn("3 Pressure structure", () => ApplyStagePreset(
            CalypsoVisualLayers.PressureStructure, "PressureStructure · shell + BH + openings")));
        panel.Children.Add(Btn("4 Complete arrangement", () => ApplyStagePreset(
            CalypsoVisualLayers.CompleteArrangement, "CompleteArrangement · full ship")));
        panel.Children.Add(Btn("HOLD cargo", ApplyPresetHoldCargo));

        panel.Children.Add(_objectBrowser);
        panel.Children.Add(Btn("Refresh object tree", () => _objectBrowser.RebuildTree()));

        panel.Children.Add(Header("Ship (full-height)"));
        panel.Children.Add(Hint("Keel→weather skeleton — frames are open rings, not walls."));
        AddLayerCheck(panel, "OML / IML shell", CalypsoVisualLayers.HullShell);
        _omlGuideCheck = new CheckBox { Content = "OML edge guide", FontSize = 11, Margin = new Thickness(2, 1) };
        GraphicalProfileBinding.Bind(_omlGuideCheck, ForegroundProperty, GraphicalProfile.TextResourceKey);
        _omlGuideCheck.IsCheckedChanged += (_, _) =>
        {
            if (_syncingUi) return;
            _controller.Session.ShowOmlGuide = _omlGuideCheck.IsChecked == true;
        };
        panel.Children.Add(_omlGuideCheck);
        AddLayerCheck(panel, "Open web frames (ring+chords+knuckles)", CalypsoVisualLayers.Frames);
        AddLayerCheck(panel, "Pressure bulkheads (closed)", CalypsoVisualLayers.Bulkheads);
        AddLayerCheck(panel, "Girders / deckbeams / knuckles", CalypsoVisualLayers.Longitudinals);

        panel.Children.Add(Header("Deck-scoped"));
        AddLayerCheck(panel, "Sandwich deck plates", CalypsoVisualLayers.DeckPlates);
        AddLayerCheck(panel, "Partition walls", CalypsoVisualLayers.Partitions);
        AddLayerCheck(panel, "Space volumes (finish)", CalypsoVisualLayers.SpaceFinish);
        AddLayerCheck(panel, "Outfit / furniture", CalypsoVisualLayers.Outfit);
        AddLayerCheck(panel, "C40 cargo (HOLD)", CalypsoVisualLayers.Cargo);
        AddLayerCheck(panel, "Openings / hatches", CalypsoVisualLayers.Openings);
        AddLayerCheck(panel, "Passage track overlay (diag)", CalypsoVisualLayers.PassagewayCutouts);
        panel.Children.Add(Hint("Overlay only — portals are cut into Ship webs/BHs."));

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
            _modeReadout.Text =
                $"Camera: {s.ViewMode}\nDisplay: {display}\nDeck: {deck}\n" +
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

            foreach (var (flag, box) in _layerChecks)
            {
                var on = s.VisualLayers.HasFlag(flag);
                if (box.IsChecked != on)
                    box.IsChecked = on;
            }

            if (_omlGuideCheck is not null && _omlGuideCheck.IsChecked != s.ShowOmlGuide)
                _omlGuideCheck.IsChecked = s.ShowOmlGuide;
        }
        finally
        {
            _syncingUi = false;
        }
    }

    private void AddLayerCheck(Panel panel, string label, CalypsoVisualLayers flag)
    {
        var box = new CheckBox
        {
            Content = label,
            IsChecked = _controller.Session.VisualLayers.HasFlag(flag),
            FontSize = 11,
            Margin = new Thickness(2, 1),
        };
        GraphicalProfileBinding.Bind(box, ForegroundProperty, GraphicalProfile.TextResourceKey);
        box.IsCheckedChanged += (_, _) =>
        {
            if (_syncingUi)
                return;
            if (box.IsChecked == true)
                _controller.Session.VisualLayers |= flag;
            else
                _controller.Session.VisualLayers &= ~flag;
            _controller.Session.StatusText = $"layer {(box.IsChecked == true ? "on" : "off")}: {label}";
        };
        _layerChecks[flag] = box;
        panel.Children.Add(box);
    }

    private StackPanel RadioRow(List<ToggleButton> store, params (string Label, Action Act)[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        for (var i = 0; i < items.Length; i++)
        {
            var idx = i;
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

    private static void SetRadio(List<ToggleButton> group, int index)
    {
        for (var i = 0; i < group.Count; i++)
        {
            var on = i == index;
            if (group[i].IsChecked != on)
                group[i].IsChecked = on;
        }
    }

    private static TextBlock Header(string text)
    {
        var t = new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 10, 0, 2),
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
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 0, 2, 4),
        };
        GraphicalProfileBinding.Bind(t, TextBlock.ForegroundProperty, GraphicalProfile.MutedResourceKey);
        return t;
    }

    private static Button Btn(string text, Action onClick)
    {
        var b = new Button
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 4),
            FontSize = 11,
            Margin = new Thickness(0, 1),
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    private static StackPanel Row(params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var c in children)
            row.Children.Add(c);
        return row;
    }
}
