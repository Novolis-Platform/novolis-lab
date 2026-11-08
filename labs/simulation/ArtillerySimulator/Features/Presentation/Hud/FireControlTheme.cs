using SkiaSharp;

namespace ArtillerySimulator;

internal static class FireControlTheme
{
    public static readonly SKColor PanelFill = new(36, 42, 52, 220);
    public static readonly SKColor PanelHotFill = new(52, 40, 36, 230);
    public static readonly SKColor Border = new(200, 220, 235, 255);
    public static readonly SKColor Text = new(245, 248, 252, 255);
    public static readonly SKColor Accent = new(255, 210, 110, 255);
    public static readonly SKColor Ready = new(140, 240, 160, 255);
    public static readonly SKColor Alert = new(255, 150, 90, 255);
    public static readonly SKColor Dim = new(190, 200, 210, 220);
    public static readonly SKColor Selected = new(120, 230, 255, 255);
}
