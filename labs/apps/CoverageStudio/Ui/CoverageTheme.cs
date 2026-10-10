using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace CoverageStudio.Ui;

/// <summary>Slate + teal engineering chrome for Coverage Studio (not purple SaaS).</summary>
internal static class CoverageTheme
{
    public static Color Canvas => GraphicalProfile.Background;
    public static Color Panel => GraphicalProfile.Surface;
    public static Color PanelAlt => GraphicalProfile.Raised;
    public static Color Border => GraphicalProfile.Border;
    public static Color Text => GraphicalProfile.Text;
    public static Color Muted => GraphicalProfile.Muted;
    public static Color Accent => GraphicalProfile.Accent;
    public static Color AccentDim => GraphicalProfile.AccentFill;
    public static Color Warn => GraphicalProfile.Warning;
    public static Color Danger => GraphicalProfile.Danger;
    public static Color Ok => GraphicalProfile.ActionSoft;

    public static IBrush CanvasBrush => GraphicalProfile.BackgroundBrush;
    public static IBrush PanelBrush => GraphicalProfile.SurfaceBrush;
    public static IBrush PanelAltBrush => GraphicalProfile.RaisedBrush;
    public static IBrush BorderBrush => GraphicalProfile.BorderBrush;
    public static IBrush TextBrush => GraphicalProfile.TextBrush;
    public static IBrush MutedBrush => GraphicalProfile.MutedBrush;
    public static IBrush AccentBrush => GraphicalProfile.AccentBrush;
    public static IBrush WarnBrush => GraphicalProfile.WarningBrush;
    public static IBrush DangerBrush => GraphicalProfile.DangerBrush;
    public static IBrush OkBrush => GraphicalProfile.ActionSoftBrush;

    public static FontFamily BodyFont => GraphicalProfile.BodyFont;
    public static FontFamily MonoFont => GraphicalProfile.MonoFont;

    public static void ApplyWindowChrome(Window window)
    {
        window.Background = CanvasBrush;
        window.Foreground = TextBrush;
        window.FontFamily = BodyFont;
    }

    public static TextBlock Title(string text, double size = 16) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = FontWeight.SemiBold,
        Foreground = TextBrush,
        FontFamily = BodyFont,
    };

    public static TextBlock Label(string text, bool muted = false) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = muted ? MutedBrush : TextBrush,
        FontFamily = BodyFont,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static TextBlock Mono(string text, double size = 12) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = TextBrush,
        FontFamily = MonoFont,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static Border PanelBox(Control child, Thickness? padding = null) => new()
    {
        Background = PanelBrush,
        BorderBrush = BorderBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = padding ?? new Thickness(8),
        Child = child,
    };

    public static Button MakeButton(string content, bool primary = false)
    {
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(12, 6),
            FontFamily = BodyFont,
            FontWeight = primary ? FontWeight.SemiBold : FontWeight.Normal,
            Background = primary ? AccentBrush : PanelAltBrush,
            Foreground = TextBrush,
            BorderBrush = primary ? new SolidColorBrush(AccentDim) : BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
        };
        return button;
    }

    public static CheckBox MakeCheck(string content, bool isChecked = false) => new()
    {
        Content = content,
        IsChecked = isChecked,
        Foreground = TextBrush,
        FontFamily = BodyFont,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static TextBox MakeTextBox(string text, double minWidth = 200) => new()
    {
        Text = text,
        MinWidth = minWidth,
        FontFamily = MonoFont,
        FontSize = 12,
        Background = PanelAltBrush,
        Foreground = TextBrush,
        BorderBrush = BorderBrush,
        CaretBrush = AccentBrush,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static NumericUpDown MakeNumeric(decimal value, decimal min, decimal max, double width = 80) => new()
    {
        Value = value,
        Minimum = min,
        Maximum = max,
        Width = width,
        FontFamily = MonoFont,
        Background = PanelAltBrush,
        Foreground = TextBrush,
        BorderBrush = BorderBrush,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
