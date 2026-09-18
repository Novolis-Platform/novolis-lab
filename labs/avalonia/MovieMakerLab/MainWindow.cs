using Avalonia.Controls;
using Novolis.Avalonia.Video;

namespace MovieMakerLab;

internal sealed class MainWindow : Window
{
    readonly MovieEditWorkspace _workspace;

    public MainWindow()
    {
        Title = "Movie Maker Lab — Full Demo";
        Width = 1180;
        Height = 760;
        Background = MovieEditPalette.Pane;

        var (project, _) = FullDemoBuilder.Build();
        _workspace = new MovieEditWorkspace(project)
        {
            HeaderTitle = "Movie Maker Lab — images · audio · transitions · titles · export",
        };
        FullDemoBuilder.WarmStills(_workspace);

        Content = _workspace;
        Closed += (_, _) => _workspace.Dispose();
    }
}
