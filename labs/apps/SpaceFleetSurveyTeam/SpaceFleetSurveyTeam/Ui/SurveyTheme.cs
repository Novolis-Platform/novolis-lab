using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace SpaceFleetSurveyTeam.Ui;

internal static class SurveyTheme
{
    public static void ApplyRoot(Panel root) =>
        root.Background = SurveyPalette.WindowBrush;

    public static TextBlock BrandTitle(string text, double size = 36) => new()
    {
        Text = text,
        FontFamily = SurveyPalette.DisplayFont,
        FontSize = size,
        FontWeight = FontWeight.SemiBold,
        Foreground = SurveyPalette.BodyBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Tagline(string text) => new()
    {
        Text = text,
        FontFamily = SurveyPalette.BodyFont,
        FontSize = 16,
        Foreground = SurveyPalette.MutedBrush,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 520,
        Margin = new Avalonia.Thickness(0, 8, 0, 0),
    };

    public static TextBlock Muted(string text, double size = 13) => new()
    {
        Text = text,
        FontFamily = SurveyPalette.BodyFont,
        FontSize = size,
        Foreground = SurveyPalette.MutedBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Label(string text, IBrush? foreground = null) => new()
    {
        Text = text,
        FontFamily = SurveyPalette.DisplayFont,
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        LetterSpacing = 1.2,
        Foreground = foreground ?? SurveyPalette.TealBrush,
    };

    public static Button Button(string text, SurveyButtonKind kind)
    {
        var button = new Button
        {
            Content = text,
            FontFamily = SurveyPalette.BodyFont,
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Padding = new Avalonia.Thickness(20, 12),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        switch (kind)
        {
            case SurveyButtonKind.Primary:
                button.Background = SurveyPalette.AmberBrush;
                button.Foreground = SurveyPalette.WindowBrush;
                break;
            case SurveyButtonKind.Secondary:
                button.Background = SurveyPalette.PanelRaisedBrush;
                button.Foreground = SurveyPalette.TealBrush;
                break;
            default:
                button.Background = Avalonia.Media.Brushes.Transparent;
                button.Foreground = SurveyPalette.MutedBrush;
                break;
        }

        return button;
    }
}
