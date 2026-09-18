using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Novolis.Avalonia.Raylib;
using Novolis.Avalonia.Rendering;

namespace RenderingAvalonia;

internal sealed class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly CpuFrameDemo _cpuDemo;

    public MainWindow()
    {
        Title = "Novolis Rendering — Avalonia hosts";
        Width = 1440;
        Height = 720;

        var twoD = new TwoDSceneControl { MinHeight = 280 };
        _ = new PlatformerDemo(twoD);

        var cpu = new Rgba32FrameControl { MinHeight = 280 };
        _cpuDemo = new CpuFrameDemo(cpu, 320, 240);

        var raylib = new RaylibHostControl
        {
            MinHeight = 280,
            FrameWidth = 320,
            FrameHeight = 240,
        };
        _ = new RaylibViewportDemo(raylib);

        var grid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            ],
            Margin = new Thickness(8),
        };
        Grid.SetColumn(twoD, 0);
        Grid.SetColumn(cpu, 1);
        Grid.SetColumn(raylib, 2);
        grid.Children.Add(twoD);
        grid.Children.Add(cpu);
        grid.Children.Add(raylib);

        Content = new DockPanel
        {
            LastChildFill = true,
            Children =
            {
                new TextBlock
                {
                    Text = "TwoD (OpenGL)  |  CPU RGBA  |  RaylibHostControl (embedded GLFW)",
                    Margin = new Thickness(8),
                    [DockPanel.DockProperty] = Dock.Top,
                },
                grid,
            },
        };

        _timer.Tick += (_, _) => _cpuDemo.Tick(0.016f);
        _timer.Start();
    }
}
