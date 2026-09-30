using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace CapitalistSimulator.Ui;

internal static class CapitalPalette
{
    public static Color Window => GraphicalProfile.Background;
    public static Color Panel => GraphicalProfile.Surface;
    public static Color PanelRaised => GraphicalProfile.Raised;
    public static Color Accent => GraphicalProfile.Action;
    public static Color AccentSoft => GraphicalProfile.Accent;
    public static Color Body => GraphicalProfile.Text;
    public static Color Muted => GraphicalProfile.Muted;
    public static Color Success => GraphicalProfile.ActionSoft;
    public static Color Danger => GraphicalProfile.Danger;
    public static readonly Color MapField = Color.Parse("#0d1822");
    public static readonly Color Road = Color.Parse("#2a3544");
    public static readonly Color Seaport = Color.Parse("#1a4060");
    public static readonly Color Bank = Color.Parse("#3a3420");
    public static readonly Color PlayerFirm = Color.Parse("#2a5a4a");
    public static readonly Color AiFirm = Color.Parse("#5a2a3a");

    public static IBrush WindowBrush => GraphicalProfile.BackgroundBrush;
    public static IBrush PanelBrush => GraphicalProfile.SurfaceBrush;
    public static IBrush PanelRaisedBrush => GraphicalProfile.RaisedBrush;
    public static IBrush AccentBrush => GraphicalProfile.ActionBrush;
    public static IBrush BodyBrush => GraphicalProfile.TextBrush;
    public static IBrush MutedBrush => GraphicalProfile.MutedBrush;
    public static IBrush SuccessBrush => GraphicalProfile.ActionSoftBrush;
    public static IBrush DangerBrush => GraphicalProfile.DangerBrush;
    public static readonly IBrush MapFieldBrush = new SolidColorBrush(MapField);

    public static FontFamily DisplayFont => GraphicalProfile.BodyFont;
    public static FontFamily BodyFont => GraphicalProfile.BodyFont;
    public static FontFamily MonoFont => GraphicalProfile.MonoFont;
}

internal enum CapitalButtonKind
{
    Primary,
    Secondary,
    Danger,
    Quiet,
}

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
                btn.Foreground = new SolidColorBrush(Color.Parse("#1a1810"));
                break;
            case CapitalButtonKind.Danger:
                btn.Background = new SolidColorBrush(Color.Parse("#3a2220"));
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
