using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Novolis.Simulation.Humanoid;
using HumanoidLab.Demo;
using HumanoidLab.Ui;

namespace HumanoidLab;

internal sealed class MainWindow : Window
{
    private readonly StickFigurePane _walkPane = new() { ClipToBounds = true };
    private readonly StickFigurePane _ragdollPane = new() { ClipToBounds = true };
    private readonly StickFigurePane _bowPane = new() { ClipToBounds = true };
    private readonly StickFigurePane _reachPane = new() { ClipToBounds = true };
    private readonly WalkDemo _walk;
    private readonly RagdollDemo _ragdoll;
    private readonly BowDemo _bow;
    private readonly ReachDemo _reach;
    private readonly DispatcherTimer _timer;
    private DateTime _lastTick = DateTime.UtcNow;

    public MainWindow()
    {
        Title = "HumanoidLab — Walk · Ragdoll · Bow · Reach";
        Width = 1480;
        Height = 720;
        Background = LabPalette.NavyBrush;

        var bind = HumanoidBindPose.CreateDefaultTPose(1.8f);
        var bank = ProceduralClips.CreateBank(bind);
        _walk = new WalkDemo(bind, bank);
        _ragdoll = new RagdollDemo(bind);
        _bow = new BowDemo(bind, bank);
        _reach = new ReachDemo(bind);

        Content = new DockPanel
        {
            LastChildFill = true,
            Children =
            {
                new Border
                {
                    [DockPanel.DockProperty] = Dock.Top,
                    Padding = new Thickness(16, 12),
                    Background = LabPalette.NavyBrush,
                    Child = new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "HumanoidLab",
                                FontSize = 22,
                                FontWeight = FontWeight.SemiBold,
                                Foreground = LabPalette.AmberBrush,
                            },
                            new TextBlock
                            {
                                Text = "Avalonia dogfood — HTTP control at http://127.0.0.1:18765/ (GET /ragdoll).",
                                FontSize = 13,
                                Foreground = LabPalette.InkBrush,
                                Opacity = 0.85,
                            },
                        },
                    },
                },
                new Grid
                {
                    Margin = new Thickness(12),
                    ColumnDefinitions = ColumnDefinitions.Parse("*,8,*,8,*,8,*"),
                    Children =
                    {
                        Pane("Walk", _walkPane, 0),
                        Pane("Ragdoll", _ragdollPane, 2),
                        Pane("Bow", _bowPane, 4),
                        Pane("Reach", _reachPane, 6),
                    },
                },
            },
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    internal RagdollDemo Ragdoll => _ragdoll;

    private static Control Pane(string title, StickFigurePane stick, int column)
    {
        var border = new Border
        {
            Background = LabPalette.PaneBrush,
            BorderBrush = LabPalette.PaneEdgeBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Child = new DockPanel
            {
                LastChildFill = true,
                Children =
                {
                    new TextBlock
                    {
                        [DockPanel.DockProperty] = Dock.Top,
                        Text = title,
                        Margin = new Thickness(12, 10, 12, 4),
                        FontSize = 15,
                        FontWeight = FontWeight.Medium,
                        Foreground = LabPalette.CopperBrush,
                    },
                    stick,
                },
            },
        };
        Grid.SetColumn(border, column);
        return border;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var dt = (float)(now - _lastTick).TotalSeconds;
        _lastTick = now;
        if (dt <= 0f || dt > 0.1f)
            dt = 1f / 60f;

        _walk.Tick(dt, _walkPane);
        _ragdoll.Tick(dt, _ragdollPane);
        _bow.Tick(dt, _bowPane);
        _reach.Tick(dt, _reachPane);
    }
}
