using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace CapitalistSimulator.Ui;

internal static class CapitalPalette
{
    public static Color Window => GraphicalProfile.Background;
    public static Color Panel => GraphicalProfile.Surface;
    public static Color PanelRaised => GraphicalProfile.Raised;
    public static Color Accent => GraphicalProfile.Action;
    public static Color AccentSoft => GraphicalProfile.Accent;
    public static Color Body => GraphicalProfile.Text;
    public static Color Muted => GraphicalProfile.Muted;
    public static Color Success => GraphicalProfile.ActionSoft;
    public static Color Danger => GraphicalProfile.Danger;
    public static readonly Color MapField = Color.Parse("#0d1822");
    public static readonly Color Road = Color.Parse("#2a3544");
    public static readonly Color Seaport = Color.Parse("#1a4060");
    public static readonly Color Bank = Color.Parse("#3a3420");
    public static readonly Color PlayerFirm = Color.Parse("#2a5a4a");
    public static readonly Color AiFirm = Color.Parse("#5a2a3a");

    public static IBrush WindowBrush => GraphicalProfile.BackgroundBrush;
    public static IBrush PanelBrush => GraphicalProfile.SurfaceBrush;
    public static IBrush PanelRaisedBrush => GraphicalProfile.RaisedBrush;
    public static IBrush AccentBrush => GraphicalProfile.ActionBrush;
    public static IBrush BodyBrush => GraphicalProfile.TextBrush;
    public static IBrush MutedBrush => GraphicalProfile.MutedBrush;
    public static IBrush SuccessBrush => GraphicalProfile.ActionSoftBrush;
    public static IBrush DangerBrush => GraphicalProfile.DangerBrush;
    public static readonly IBrush MapFieldBrush = new SolidColorBrush(MapField);

    public static FontFamily DisplayFont => GraphicalProfile.BodyFont;
    public static FontFamily BodyFont => GraphicalProfile.BodyFont;
    public static FontFamily MonoFont => GraphicalProfile.MonoFont;
}
