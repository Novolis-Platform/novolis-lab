using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CalypsoCad.Host;
using CalypsoCad.Rendering;
using Novolis.Avalonia.GraphicalProfile;

namespace CalypsoCad.Shell;

internal sealed class CalypsoExplorerWindow : Window
{
    private readonly CalypsoViewportController _controller;
    private readonly TextBlock _status;
    private readonly DispatcherTimer _statusTimer;

    public CalypsoExplorerWindow(CalypsoViewportController controller)
    {
        _controller = controller;
        Title = "Calypso CAD Explorer";
        Width = 1480;
        Height = 900;
        FontFamily = GraphicalProfile.BodyFont;

        GraphicalProfileBinding.Bind(this, BackgroundProperty, GraphicalProfile.BackgroundResourceKey);

        var viewport = new CalypsoSilkViewportControl
        {
            Controller = controller,
            FrameWidth = 960,
            FrameHeight = 640,
            [DockPanel.DockProperty] = Dock.Right,
        };

        _status = new TextBlock
        {
            Margin = new Thickness(8, 4),
            FontFamily = GraphicalProfile.MonoFont,
            FontSize = 12,
        };
        GraphicalProfileBinding.Bind(_status, TextBlock.ForegroundProperty, GraphicalProfile.MutedResourceKey);

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

        _statusTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) =>
        {
            var s = _controller.Session;
            _status.Text =
                $"{s.ViewMode} · {s.WireMeshMode} · deck={(s.DeckFilter?.ToString() ?? "all")} · " +
                $"cut={(s.CutPlaneLongitudinal ? "L" : "B")}@{s.CutPlaneOffset:0.#} · {s.StatusText}";
        });
        _statusTimer.Start();
        Closed += (_, _) => _statusTimer.Stop();
    }

    private Control BuildChrome()
    {
        var panel = new StackPanel
        {
            Width = 200,
            Margin = new Thickness(8),
            Spacing = 6,
        };
        GraphicalProfileBinding.Bind(panel, BackgroundProperty, GraphicalProfile.SurfaceResourceKey);

        panel.Children.Add(Header("View"));
        panel.Children.Add(Row(
            Btn("Plan", () => _controller.SetView(CalypsoViewMode.Plan)),
            Btn("Orbit", () => _controller.SetView(CalypsoViewMode.Orbit)),
            Btn("Interior", () => _controller.SetInterior())));

        panel.Children.Add(Header("Mesh"));
        panel.Children.Add(Row(
            Btn("Solid", () => _controller.SetWire(CalypsoWireMeshMode.None)),
            Btn("Wire", () => _controller.SetWire(CalypsoWireMeshMode.Wire)),
            Btn("Cut", () => _controller.SetWire(CalypsoWireMeshMode.CutawayPartial))));

        panel.Children.Add(Header("Deck"));
        panel.Children.Add(Row(
            Btn("−1", () => _controller.SetDeckFilter(-1)),
            Btn("0", () => _controller.SetDeckFilter(0)),
            Btn("+1", () => _controller.SetDeckFilter(1)),
            Btn("All", () => _controller.SetDeckFilter(null))));

        panel.Children.Add(Header("Layers / cut"));
        panel.Children.Add(Btn("Cycle layers (G)", () => _controller.CycleLayerPreset()));
        panel.Children.Add(Btn("OML guide (H)", () => _controller.ToggleOmlGuide()));
        panel.Children.Add(Row(
            Btn("Cut L", () => _controller.SetCutLongitudinal(true)),
            Btn("Cut B", () => _controller.SetCutLongitudinal(false))));
        panel.Children.Add(Row(
            Btn("Cut −", () => _controller.NudgeCut(-1f)),
            Btn("Cut +", () => _controller.NudgeCut(1f))));

        panel.Children.Add(Header("Session"));
        panel.Children.Add(Btn("Regenerate", () => _controller.Regenerate()));
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

        var border = new Border
        {
            Child = panel,
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(4),
        };
        GraphicalProfileBinding.Bind(border, Border.BackgroundProperty, GraphicalProfile.SurfaceResourceKey);
        GraphicalProfileBinding.Bind(border, Border.BorderBrushProperty, GraphicalProfile.BorderResourceKey);
        border.BorderThickness = new Thickness(1);
        return border;
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

    private static Button Btn(string text, Action onClick)
    {
        var b = new Button
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 4),
            FontSize = 11,
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    private static StackPanel Row(params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var c in children)
        {
            if (c is Button b)
                b.HorizontalAlignment = HorizontalAlignment.Left;
            row.Children.Add(c);
        }

        return row;
    }
}
