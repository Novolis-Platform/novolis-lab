using Avalonia.Media;
using Profile = Novolis.Avalonia.GraphicalProfile.GraphicalProfile;

namespace HumanoidLab.Ui;

internal static class LabPalette
{
    public static Color Navy => Profile.Background;
    public static Color Teal => Profile.AccentFill;
    public static Color TealBright => Profile.Accent;
    public static Color Copper => Profile.Warning;
    public static Color Amber => Profile.Action;
    public static Color Ink => Profile.Text;
    public static Color Pane => Profile.Surface;
    public static Color PaneEdge => Profile.Border;

    public static IBrush NavyBrush => Profile.BackgroundBrush;
    public static IBrush TealBrush => Profile.AccentFillBrush;
    public static IBrush TealBrightBrush => Profile.AccentBrush;
    public static IBrush CopperBrush => Profile.WarningBrush;
    public static IBrush AmberBrush => Profile.ActionBrush;
    public static IBrush InkBrush => Profile.TextBrush;
    public static IBrush PaneBrush => Profile.SurfaceBrush;
    public static IBrush PaneEdgeBrush => Profile.BorderBrush;
}
