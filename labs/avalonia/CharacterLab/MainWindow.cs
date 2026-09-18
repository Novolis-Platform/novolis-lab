using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CharacterLab.Demo;
using CharacterLab.Ui;
using Novolis.Avalonia.ThreeD;
using Novolis.Avalonia.ThreeD.Session;
using Novolis.Avalonia.ThreeD.Ui;

namespace CharacterLab;

/// <summary>CMU BVH mocap player: 3D wire mannequin + Front/Side sticks + hold-point rifle.</summary>
internal sealed class MainWindow : Window
{
    readonly StickFigurePane _front = new() { ClipToBounds = true };
    readonly StickFigurePane _side = new() { ClipToBounds = true };
    readonly MocapParadeDriver _driver;
    readonly WireMannequinScene _mannequin;
    readonly SceneViewportControl _viewport;
    readonly TextBlock _phaseLabel = new()
    {
        FontSize = 15,
        FontWeight = FontWeight.SemiBold,
        Foreground = LabPalette.AmberBrush,
        TextWrapping = TextWrapping.Wrap,
    };
    readonly ComboBox _clipBox = new()
    {
        MinWidth = 180,
        HorizontalAlignment = HorizontalAlignment.Left,
    };
    readonly Slider _scrub = new()
    {
        Minimum = 0,
        Maximum = 1,
        Value = 0,
        Width = 280,
    };
    readonly CheckBox _holdCheck = new()
    {
        Content = "Hold locks",
        IsChecked = true,
        Foreground = LabPalette.InkBrush,
    };
    readonly Button _pauseBtn = new()
    {
        Content = "Pause",
        Padding = new Thickness(12, 6),
    };
    readonly DispatcherTimer _timer;
    DateTime _lastTick = DateTime.UtcNow;
    bool _scrubbing;

    public MainWindow(MocapParadeDriver driver, SceneSessionService session)
    {
        _driver = driver;
        _mannequin = new WireMannequinScene(session);
        _viewport = new SceneViewportControl(session, SceneViewportBackendKind.OpenGl)
        {
            MinHeight = 360,
        };

        Title = "CharacterLab — mocap wire mannequin";
        Width = 1440;
        Height = 900;
        Background = LabPalette.NavyBrush;

        foreach (var clip in _driver.Clips)
            _clipBox.Items.Add(new ComboBoxItem { Content = $"{clip.Label} ({clip.Source})", Tag = clip.Id });
        if (_clipBox.Items.Count > 0)
            _clipBox.SelectedIndex = 0;

        _clipBox.SelectionChanged += (_, _) =>
        {
            if (_clipBox.SelectedItem is ComboBoxItem { Tag: string id })
            {
                _driver.SelectClip(id);
                SyncScrubRange();
            }
        };
        _scrub.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty || _scrubbing)
                return;
            _scrubbing = true;
            _driver.Paused = true;
            _driver.Seek((float)_scrub.Value);
            _pauseBtn.Content = "Resume";
            _scrubbing = false;
        };
        _holdCheck.IsCheckedChanged += (_, _) =>
            _driver.HoldMode = _holdCheck.IsChecked == true;
        _pauseBtn.Click += (_, _) =>
        {
            _driver.Paused = !_driver.Paused;
            _pauseBtn.Content = _driver.Paused ? "Resume" : "Pause";
        };

        SyncScrubRange();

        Content = new DockPanel
        {
            LastChildFill = true,
            Children =
            {
                new Border
                {
                    [DockPanel.DockProperty] = Dock.Top,
                    Padding = new Thickness(18, 12),
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "CharacterLab",
                                FontSize = 26,
                                FontWeight = FontWeight.SemiBold,
                                Foreground = LabPalette.AmberBrush,
                            },
                            new TextBlock
                            {
                                Text = "CMU BVH mocap → wire capsule mannequin + HumanoidFullBodyIk hold locks. Mesh FBX off until wire reads. Agent :18795",
                                FontSize = 13,
                                Foreground = LabPalette.InkBrush,
                                Opacity = 0.92,
                                TextWrapping = TextWrapping.Wrap,
                            },
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Spacing = 12,
                                Children =
                                {
                                    new TextBlock
                                    {
                                        Text = "Clip",
                                        VerticalAlignment = VerticalAlignment.Center,
                                        Foreground = LabPalette.CopperBrush,
                                    },
                                    _clipBox,
                                    _pauseBtn,
                                    _holdCheck,
                                    new TextBlock
                                    {
                                        Text = "Seek",
                                        VerticalAlignment = VerticalAlignment.Center,
                                        Foreground = LabPalette.CopperBrush,
                                    },
                                    _scrub,
                                },
                            },
                            _phaseLabel,
                        },
                    },
                },
                new Grid
                {
                    Margin = new Thickness(12, 0, 12, 12),
                    RowDefinitions = RowDefinitions.Parse("2*,10,*"),
                    ColumnDefinitions = ColumnDefinitions.Parse("*,10,*"),
                    Children =
                    {
                        ViewportPane(),
                        StickPane("Front (XY)", _front, 2, 0),
                        StickPane("Side (ZY)", _side, 2, 2),
                    },
                },
            },
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
        Closed += (_, _) => _timer.Stop();

        Opened += (_, _) =>
        {
            _viewport.Fit();
            _viewport.RequestPresent();
        };
    }

    Control ViewportPane()
    {
        var border = new Border
        {
            Background = LabPalette.PaneBrush,
            BorderBrush = LabPalette.PaneEdgeBrush,
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = new DockPanel
            {
                LastChildFill = true,
                Children =
                {
                    new TextBlock
                    {
                        [DockPanel.DockProperty] = Dock.Top,
                        Text = "3D wire mannequin (orbit)",
                        Margin = new Thickness(12, 10, 12, 4),
                        FontSize = 15,
                        FontWeight = FontWeight.Medium,
                        Foreground = LabPalette.CopperBrush,
                    },
                    _viewport,
                },
            },
        };
        Grid.SetRow(border, 0);
        Grid.SetColumn(border, 0);
        Grid.SetColumnSpan(border, 3);
        return border;
    }

    static Control StickPane(string title, StickFigurePane stick, int row, int column)
    {
        var border = new Border
        {
            Background = LabPalette.PaneBrush,
            BorderBrush = LabPalette.PaneEdgeBrush,
            BorderThickness = new Thickness(1),
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
        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        return border;
    }

    void SyncScrubRange()
    {
        _scrubbing = true;
        _scrub.Minimum = 0;
        _scrub.Maximum = Math.Max(_driver.DurationSeconds, 0.001);
        _scrub.Value = _driver.TimeSeconds;
        _scrubbing = false;
    }

    void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var dt = (float)(now - _lastTick).TotalSeconds;
        _lastTick = now;
        if (dt <= 0f || dt > 0.1f)
            dt = 1f / 60f;

        _driver.Tick(dt);
        _mannequin.Update(_driver);
        _driver.Paint(_front, _side);
        _viewport.RequestPresent();

        if (!_scrubbing && !_driver.Paused)
        {
            _scrubbing = true;
            _scrub.Value = _driver.TimeSeconds;
            _scrubbing = false;
        }

        var holds = _driver.SampleHolds();
        var clip = _driver.ActiveClip;
        var pause = _driver.Paused ? "  ·  PAUSED" : "";
        _phaseLabel.Text =
            $"{clip?.Label ?? _driver.ActiveClipId}  ·  source={_driver.SkinSource}  ·  {_driver.Phase}  ·  t={_driver.TimeSeconds:0.00}/{_driver.DurationSeconds:0.00}s  ·  gripΔ r={holds.RightHandError:0.000} l={holds.LeftHandError:0.000}{pause}";
    }
}
