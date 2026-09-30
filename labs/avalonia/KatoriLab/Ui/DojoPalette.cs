using Avalonia.Media;
using Profile = Novolis.Avalonia.GraphicalProfile.GraphicalProfile;

namespace KatoriLab.Ui;

internal static class DojoPalette
{
    public static Color InkFloor => Profile.Background;
    public static Color Tatami => Profile.Surface;
    public static Color Bamboo => Profile.ActionSoft;
    public static Color BambooBright => Profile.Accent;
    public static Color Lacquer => Profile.Danger;
    public static Color Gold => Profile.Warning;
    public static Color Washi => Profile.Text;
    public static Color Pane => Profile.Raised;
    public static Color PaneEdge => Profile.Border;

    public static IBrush InkFloorBrush => Profile.BackgroundBrush;
    public static IBrush TatamiBrush => Profile.SurfaceBrush;
    public static IBrush BambooBrush => Profile.ActionSoftBrush;
    public static IBrush BambooBrightBrush => Profile.AccentBrush;
    public static IBrush LacquerBrush => Profile.DangerBrush;
    public static IBrush GoldBrush => Profile.WarningBrush;
    public static IBrush WashiBrush => Profile.TextBrush;
    public static IBrush PaneBrush => Profile.RaisedBrush;
    public static IBrush PaneEdgeBrush => Profile.BorderBrush;
}
