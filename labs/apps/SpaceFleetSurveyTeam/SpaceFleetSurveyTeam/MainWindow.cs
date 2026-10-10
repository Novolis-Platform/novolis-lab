using Avalonia.Controls;
using SpaceFleetSurveyTeam.Ui;

namespace SpaceFleetSurveyTeam;

/// <summary>Desktop shell hosting <see cref="Views.FieldShellView"/>.</summary>
public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Space Fleet: Survey Team";
        Width = 960;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = SurveyPalette.WindowBrush;
        FontFamily = SurveyPalette.BodyFont;
        Foreground = SurveyPalette.BodyBrush;
    }
}
