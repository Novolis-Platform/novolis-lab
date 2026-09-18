using Avalonia.Media;

namespace FriendLab.Ui;

internal static class FriendPalette
{
    public static readonly Color Pine = Color.Parse("#1e3a2f");
    public static readonly Color PineDeep = Color.Parse("#13261f");
    public static readonly Color Mist = Color.Parse("#d7e3dc");
    public static readonly Color MistSoft = Color.Parse("#eef4f0");
    public static readonly Color Ink = Color.Parse("#14201b");
    public static readonly Color InkMuted = Color.Parse("#3d5248");
    public static readonly Color Signal = Color.Parse("#d4a012");
    public static readonly Color SignalDeep = Color.Parse("#a67c00");
    public static readonly Color Panel = Color.Parse("#f4f8f5");
    public static readonly Color Edge = Color.Parse("#9bb5a6");

    public static readonly IBrush PineBrush = new SolidColorBrush(Pine);
    public static readonly IBrush PineDeepBrush = new SolidColorBrush(PineDeep);
    public static readonly IBrush MistBrush = new SolidColorBrush(Mist);
    public static readonly IBrush MistSoftBrush = new SolidColorBrush(MistSoft);
    public static readonly IBrush InkBrush = new SolidColorBrush(Ink);
    public static readonly IBrush InkMutedBrush = new SolidColorBrush(InkMuted);
    public static readonly IBrush SignalBrush = new SolidColorBrush(Signal);
    public static readonly IBrush SignalDeepBrush = new SolidColorBrush(SignalDeep);
    public static readonly IBrush PanelBrush = new SolidColorBrush(Panel);
    public static readonly IBrush EdgeBrush = new SolidColorBrush(Edge);

    public static readonly FontFamily Display = new("Georgia, 'Palatino Linotype', Palatino, serif");
    public static readonly FontFamily Body = new("Candara, Calibri, 'Segoe UI', sans-serif");
}
