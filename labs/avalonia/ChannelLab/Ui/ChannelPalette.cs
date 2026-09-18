using Avalonia.Media;

namespace ChannelLab.Ui;

internal static class ChannelPalette
{
    public static readonly Color Navy = Color.Parse("#0f1c2e");
    public static readonly Color NavyDeep = Color.Parse("#0a1422");
    public static readonly Color Panel = Color.Parse("#162538");
    public static readonly Color PanelLift = Color.Parse("#1d3148");
    public static readonly Color Mist = Color.Parse("#d9e4f0");
    public static readonly Color MistSoft = Color.Parse("#eef3f8");
    public static readonly Color InkMuted = Color.Parse("#8aa0b8");
    public static readonly Color Copper = Color.Parse("#c9853a");
    public static readonly Color Teal = Color.Parse("#3a9e8f");
    public static readonly Color Edge = Color.Parse("#2a415c");

    public static readonly IBrush NavyBrush = new SolidColorBrush(Navy);
    public static readonly IBrush NavyDeepBrush = new SolidColorBrush(NavyDeep);
    public static readonly IBrush PanelBrush = new SolidColorBrush(Panel);
    public static readonly IBrush PanelLiftBrush = new SolidColorBrush(PanelLift);
    public static readonly IBrush MistBrush = new SolidColorBrush(Mist);
    public static readonly IBrush MistSoftBrush = new SolidColorBrush(MistSoft);
    public static readonly IBrush InkMutedBrush = new SolidColorBrush(InkMuted);
    public static readonly IBrush CopperBrush = new SolidColorBrush(Copper);
    public static readonly IBrush TealBrush = new SolidColorBrush(Teal);
    public static readonly IBrush EdgeBrush = new SolidColorBrush(Edge);

    public static readonly FontFamily Display = new("Georgia, 'Palatino Linotype', Palatino, serif");
    public static readonly FontFamily Body = new("Candara, Calibri, 'Segoe UI', sans-serif");
    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas, 'Courier New', monospace");
}
