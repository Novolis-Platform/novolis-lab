using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace CapitalistSimulator.Ui;

internal static class CapitalTheme
{
    public static void ApplyWindowChrome(Window window)
    {
        window.Background = CapitalPalette.WindowBrush;
        window.FontFamily = CapitalPalette.BodyFont;
        window.Foreground = CapitalPalette.BodyBrush;
    }

    public static Button MakeButton(string text, CapitalButtonKind kind = CapitalButtonKind.Secondary)
    {
        var btn = new Button
        {
            Content = text,
            Padding = new Thickness(12, 6),
            Margin = new Thickness(0, 0, 6, 4),
            FontFamily = CapitalPalette.BodyFont,
            FontSize = kind == CapitalButtonKind.Primary ? 13 : 12,
            FontWeight = kind == CapitalButtonKind.Primary ? FontWeight.SemiBold : FontWeight.Normal,
            CornerRadius = new CornerRadius(3),
        };
        switch (kind)
        {
            case CapitalButtonKind.Primary:
                btn.Background = CapitalPalette.AccentBrush;
                btn.Foreground = GraphicalProfile.OnActionBrush;
                break;
            case CapitalButtonKind.Danger:
                btn.Background = CapitalPalette.PanelRaisedBrush;
                btn.Foreground = CapitalPalette.DangerBrush;
                break;
            case CapitalButtonKind.Quiet:
                btn.Background = Brushes.Transparent;
                btn.Foreground = CapitalPalette.MutedBrush;
                break;
            default:
                btn.Background = CapitalPalette.PanelRaisedBrush;
                btn.Foreground = CapitalPalette.BodyBrush;
                break;
        }
        return btn;
    }

    public static TextBlock Title(string text, double size = 22) => new()
    {
        Text = text,
        FontFamily = CapitalPalette.DisplayFont,
        FontSize = size,
        FontWeight = FontWeight.SemiBold,
        Foreground = CapitalPalette.AccentBrush,
    };

    public static TextBlock Label(string text, bool muted = false) => new()
    {
        Text = text,
        FontFamily = CapitalPalette.BodyFont,
        FontSize = 12,
        Foreground = muted ? CapitalPalette.MutedBrush : CapitalPalette.BodyBrush,
        Margin = new Thickness(0, 0, 0, 4),
    };

    public static TextBlock Mono(string text, double size = 12) => new()
    {
        Text = text,
        FontFamily = CapitalPalette.MonoFont,
        FontSize = size,
        Foreground = CapitalPalette.BodyBrush,
    };

    public static Border Section(string title, Control child) => new()
    {
        Background = CapitalPalette.PanelBrush,
        BorderBrush = CapitalPalette.PanelRaisedBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(10),
        Margin = new Thickness(0, 0, 0, 8),
        Child = new StackPanel
        {
            Children =
            {
                Label(title, muted: true),
                child,
            },
        },
    };

    public static Border MetricChip(string label, string value) => new()
    {
        Background = CapitalPalette.PanelRaisedBrush,
        CornerRadius = new CornerRadius(3),
        Padding = new Thickness(10, 6),
        Margin = new Thickness(0, 0, 8, 4),
        Child = new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    FontSize = 10,
                    Foreground = CapitalPalette.MutedBrush,
                    FontFamily = CapitalPalette.BodyFont,
                },
                new TextBlock
                {
                    Text = value,
                    FontSize = 14,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = CapitalPalette.BodyBrush,
                    FontFamily = CapitalPalette.MonoFont,
                },
            },
        },
    };
}
