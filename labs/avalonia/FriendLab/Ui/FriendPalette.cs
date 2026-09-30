using Avalonia.Media;
using Profile = Novolis.Avalonia.GraphicalProfile.GraphicalProfile;

namespace FriendLab.Ui;

internal static class FriendPalette
{
    public static Color Pine => Profile.Surface;
    public static Color PineDeep => Profile.Background;
    public static Color Mist => Profile.Text;
    public static Color MistSoft => Profile.OnAccentFill;
    public static Color Ink => Profile.Text;
    public static Color InkMuted => Profile.Muted;
    public static Color Signal => Profile.Action;
    public static Color SignalDeep => Profile.AccentFill;
    public static Color Panel => Profile.Raised;
    public static Color Edge => Profile.Border;

    public static IBrush PineBrush => Profile.SurfaceBrush;
    public static IBrush PineDeepBrush => Profile.BackgroundBrush;
    public static IBrush MistBrush => Profile.TextBrush;
    public static IBrush MistSoftBrush => Profile.OnAccentFillBrush;
    public static IBrush InkBrush => Profile.TextBrush;
    public static IBrush InkMutedBrush => Profile.MutedBrush;
    public static IBrush SignalBrush => Profile.ActionBrush;
    public static IBrush SignalDeepBrush => Profile.AccentFillBrush;
    public static IBrush PanelBrush => Profile.RaisedBrush;
    public static IBrush EdgeBrush => Profile.BorderBrush;

    public static FontFamily Display => Profile.BodyFont;
    public static FontFamily Body => Profile.BodyFont;
}
