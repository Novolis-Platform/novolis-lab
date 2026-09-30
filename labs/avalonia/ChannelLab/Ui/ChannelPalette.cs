using Avalonia.Media;
using Profile = Novolis.Avalonia.GraphicalProfile.GraphicalProfile;

namespace ChannelLab.Ui;

internal static class ChannelPalette
{
    public static Color Navy => Profile.Surface;
    public static Color NavyDeep => Profile.Background;
    public static Color Panel => Profile.Surface;
    public static Color PanelLift => Profile.Raised;
    public static Color Mist => Profile.Text;
    public static Color MistSoft => Profile.OnAccentFill;
    public static Color InkMuted => Profile.Muted;
    public static Color Copper => Profile.Warning;
    public static Color Teal => Profile.Accent;
    public static Color Edge => Profile.Border;

    public static IBrush NavyBrush => Profile.SurfaceBrush;
    public static IBrush NavyDeepBrush => Profile.BackgroundBrush;
    public static IBrush PanelBrush => Profile.SurfaceBrush;
    public static IBrush PanelLiftBrush => Profile.RaisedBrush;
    public static IBrush MistBrush => Profile.TextBrush;
    public static IBrush MistSoftBrush => Profile.OnAccentFillBrush;
    public static IBrush InkMutedBrush => Profile.MutedBrush;
    public static IBrush CopperBrush => Profile.WarningBrush;
    public static IBrush TealBrush => Profile.AccentBrush;
    public static IBrush EdgeBrush => Profile.BorderBrush;

    public static FontFamily Display => Profile.BodyFont;
    public static FontFamily Body => Profile.BodyFont;
    public static FontFamily Mono => Profile.MonoFont;
}
