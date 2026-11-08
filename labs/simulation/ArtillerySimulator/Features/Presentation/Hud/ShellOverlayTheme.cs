using SkiaSharp;

namespace ArtillerySimulator;

internal static class ShellOverlayTheme
{
    public static readonly SKTypeface Typeface =
        SKTypeface.FromFamilyName("Cascadia Mono", SKFontStyle.Bold)
        ?? SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold)
        ?? SKTypeface.FromFamilyName("Courier New", SKFontStyle.Bold)
        ?? SKTypeface.Default;

    public static readonly SKColor CanvasClear = new(12, 14, 16);
    public static readonly SKColor Title = new(232, 214, 180);
    public static readonly SKColor Subtitle = new(180, 196, 170);
    public static readonly SKColor Accent = new(210, 132, 72);
    public static readonly SKColor Muted = new(140, 148, 140);
    public static readonly SKColor Body = new(200, 208, 196);
    public static readonly SKColor BodyDim = new(190, 198, 186);
    public static readonly SKColor PanelFill = new(22, 28, 24, 235);
    public static readonly SKColor CardFill = new(18, 24, 20, 210);
    public static readonly SKColor DimShade = new(0, 0, 0, 160);
    public static readonly SKColor SplashShade = new(0, 0, 0, 70);
}
