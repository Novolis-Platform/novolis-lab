using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SpaceFleetSurveyTeam.Ui;

namespace SpaceFleetSurveyTeam.Views;

/// <summary>
/// Mobile-native field shell: brand, survey CTA, loop phases, and sensor stubs.
/// Live instrument widgets arrive later via Novolis.Avalonia packages.
/// </summary>
public sealed class FieldShellView : UserControl
{
    enum SurveyPhase
    {
        Survey,
        Detect,
        Resolve,
        Certify,
    }

    static readonly string[] SensorNames =
    [
        "Sonic",
        "Photonic",
        "Magnetic",
        "Spatial",
    ];

    readonly TextBlock _phaseLabel = SurveyTheme.Muted("Loop: Survey", 13);
    readonly TextBlock _status = SurveyTheme.Muted(
        "Sensors idle — platform instrument packages not wired yet.",
        12);
    readonly StackPanel _phaseRow = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
    };

    SurveyPhase _phase = SurveyPhase.Survey;

    public FieldShellView()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
        };
        SurveyTheme.ApplyRoot(root);
        Background = SurveyPalette.WindowBrush;

        BuildPhaseChips();

        var hero = BuildHero();
        Grid.SetRow(hero, 0);
        root.Children.Add(hero);

        var instruments = BuildInstrumentStrip();
        Grid.SetRow(instruments, 1);
        root.Children.Add(instruments);

        Content = root;
        RefreshPhaseChrome();
    }

    void BuildPhaseChips()
    {
        foreach (SurveyPhase phase in Enum.GetValues<SurveyPhase>())
        {
            _phaseRow.Children.Add(new Border
            {
                Background = SurveyPalette.PanelRaisedBrush,
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(10, 6),
                Tag = phase,
                Child = new TextBlock
                {
                    Text = phase.ToString(),
                    FontFamily = SurveyPalette.BodyFont,
                    FontSize = 12,
                    Foreground = SurveyPalette.MutedBrush,
                },
            });
        }
    }

    Control BuildHero()
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(28, 36, 28, 16),
            Spacing = 0,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 560,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var canvas = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                [
                    new GradientStop(Color.Parse("#061018"), 0),
                    new GradientStop(Color.Parse("#0a2430"), 0.45),
                    new GradientStop(Color.Parse("#0c1c28"), 1),
                ],
            },
            Child = stack,
        };

        stack.Children.Add(SurveyTheme.Label("FIELD INSTRUMENT", SurveyPalette.TealBrush));

        stack.Children.Add(new TextBlock
        {
            Text = "Space Fleet",
            FontFamily = SurveyPalette.DisplayFont,
            FontSize = 42,
            FontWeight = FontWeight.Bold,
            Foreground = SurveyPalette.BodyBrush,
            Margin = new Thickness(0, 10, 0, 0),
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Survey Team",
            FontFamily = SurveyPalette.DisplayFont,
            FontSize = 42,
            FontWeight = FontWeight.Bold,
            Foreground = SurveyPalette.AmberBrush,
            Margin = new Thickness(0, -4, 0, 0),
        });

        stack.Children.Add(SurveyTheme.Tagline(
            "Use real-world field data to make one more part of an unknown planet known."));

        var start = SurveyTheme.Button("Start survey", SurveyButtonKind.Primary);
        start.Margin = new Thickness(0, 28, 0, 0);
        start.Click += (_, _) => AdvancePhase();
        stack.Children.Add(start);

        _phaseLabel.Margin = new Thickness(0, 18, 0, 0);
        stack.Children.Add(_phaseLabel);

        _status.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(_status);

        _phaseRow.Margin = new Thickness(0, 22, 0, 0);
        stack.Children.Add(_phaseRow);

        return canvas;
    }

    Control BuildInstrumentStrip()
    {
        var strip = new Grid
        {
            Margin = new Thickness(16, 0, 16, 16),
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
        };

        for (var i = 0; i < SensorNames.Length; i++)
        {
            var cell = new Border
            {
                Background = SurveyPalette.PanelBrush,
                BorderBrush = SurveyPalette.UncertainBrush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(4),
                Padding = new Thickness(12, 14),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        SurveyTheme.Label(SensorNames[i].ToUpperInvariant(), SurveyPalette.TealBrush),
                        SurveyTheme.Muted("Stub — awaiting platform gizmo", 11),
                    },
                },
            };
            Grid.SetColumn(cell, i);
            strip.Children.Add(cell);
        }

        return strip;
    }

    void AdvancePhase()
    {
        _phase = _phase switch
        {
            SurveyPhase.Survey => SurveyPhase.Detect,
            SurveyPhase.Detect => SurveyPhase.Resolve,
            SurveyPhase.Resolve => SurveyPhase.Certify,
            _ => SurveyPhase.Survey,
        };

        RefreshPhaseChrome();
        _status.Text = _phase == SurveyPhase.Certify
            ? "Certification placeholder — region would mark complete when evidence is sufficient."
            : $"Phase {_phase}: gather representative samples; quality over coverage.";
    }

    void RefreshPhaseChrome()
    {
        _phaseLabel.Text = $"Loop: {_phase}";

        foreach (var child in _phaseRow.Children)
        {
            if (child is not Border chip || chip.Tag is not SurveyPhase phase || chip.Child is not TextBlock label)
                continue;

            var active = phase == _phase;
            chip.Background = active ? SurveyPalette.PanelRaisedBrush : SurveyPalette.PanelBrush;
            chip.BorderBrush = active ? SurveyPalette.AmberBrush : SurveyPalette.UncertainBrush;
            chip.BorderThickness = new Thickness(active ? 1.5 : 1);
            label.Foreground = active ? SurveyPalette.AmberBrush : SurveyPalette.MutedBrush;
        }
    }
}
