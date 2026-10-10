using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace CursorRemote.Ui;

internal static class RemoteTheme
{
    public static TextBlock Title(string text, double size = 32) => new()
    {
        Text = text,
        FontFamily = RemotePalette.DisplayFont,
        FontSize = size,
        FontWeight = FontWeight.SemiBold,
        Foreground = RemotePalette.BodyBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Body(string text, double size = 14) => new()
    {
        Text = text,
        FontFamily = RemotePalette.BodyFont,
        FontSize = size,
        Foreground = RemotePalette.BodyBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Muted(string text, double size = 13) => new()
    {
        Text = text,
        FontFamily = RemotePalette.BodyFont,
        FontSize = size,
        Foreground = RemotePalette.MutedBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Label(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontFamily = RemotePalette.DisplayFont,
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        LetterSpacing = 1.2,
        Foreground = RemotePalette.TealBrush,
    };

    public static Button Button(string text, RemoteButtonKind kind) =>
        new()
        {
            Content = text,
            FontFamily = RemotePalette.BodyFont,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Padding = new Avalonia.Thickness(16, 10),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            Background = kind switch
            {
                RemoteButtonKind.Primary => RemotePalette.AmberBrush,
                RemoteButtonKind.Secondary => RemotePalette.PanelRaisedBrush,
                _ => Brushes.Transparent,
            },
            Foreground = kind switch
            {
                RemoteButtonKind.Primary => RemotePalette.WindowBrush,
                RemoteButtonKind.Secondary => RemotePalette.TealBrush,
                _ => RemotePalette.MutedBrush,
            },
            HorizontalAlignment = HorizontalAlignment.Left,
        };

    public static TextBox TextBox(string? watermark = null) =>
        new()
        {
            FontFamily = RemotePalette.BodyFont,
            FontSize = 14,
            Padding = new Avalonia.Thickness(10, 8),
            Background = RemotePalette.PanelRaisedBrush,
            Foreground = RemotePalette.BodyBrush,
            BorderBrush = RemotePalette.UncertainBrush,
            PlaceholderText = watermark,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
}
