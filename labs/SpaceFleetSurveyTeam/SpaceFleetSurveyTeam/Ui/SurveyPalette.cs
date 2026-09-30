using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace SpaceFleetSurveyTeam.Ui;

/// <summary>
/// Field-instrument tokens — deep navy / teal canvas with copper amber accent.
/// Not Inter, not purple SaaS, not cream+terracotta broadsheet.
/// </summary>
internal static class SurveyPalette
{
    public static Color Window => GraphicalProfile.Background;
    public static Color Panel => GraphicalProfile.Surface;
    public static Color PanelRaised => GraphicalProfile.Raised;
    public static Color Teal => GraphicalProfile.Accent;
    public static Color Amber => GraphicalProfile.Action;
    public static Color Body => GraphicalProfile.Text;
    public static Color Muted => GraphicalProfile.Muted;
    public static Color Uncertain => GraphicalProfile.Border;

    public static IBrush WindowBrush => GraphicalProfile.BackgroundBrush;
    public static IBrush PanelBrush => GraphicalProfile.SurfaceBrush;
    public static IBrush PanelRaisedBrush => GraphicalProfile.RaisedBrush;
    public static IBrush TealBrush => GraphicalProfile.AccentBrush;
    public static IBrush AmberBrush => GraphicalProfile.ActionBrush;
    public static IBrush BodyBrush => GraphicalProfile.TextBrush;
    public static IBrush MutedBrush => GraphicalProfile.MutedBrush;
    public static IBrush UncertainBrush => GraphicalProfile.BorderBrush;

    public static FontFamily DisplayFont => GraphicalProfile.BodyFont;
    public static FontFamily BodyFont => GraphicalProfile.BodyFont;
    public static FontFamily MonoFont => GraphicalProfile.MonoFont;
}
