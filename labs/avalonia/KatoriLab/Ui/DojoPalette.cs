using Avalonia.Media;

namespace KatoriLab.Ui;

/// <summary>Dojo ink / lacquer / bamboo — not CharacterLab navy-teal, not purple SaaS.</summary>
internal static class DojoPalette
{
    public static readonly Color InkFloor = Color.Parse("#12151A");
    public static readonly Color Tatami = Color.Parse("#1C2420");
    public static readonly Color Bamboo = Color.Parse("#3F6B4A");
    public static readonly Color BambooBright = Color.Parse("#6FA87A");
    public static readonly Color Lacquer = Color.Parse("#B33A2B");
    public static readonly Color Gold = Color.Parse("#C9A24A");
    public static readonly Color Washi = Color.Parse("#E8E2D6");
    public static readonly Color Pane = Color.Parse("#1A1F24");
    public static readonly Color PaneEdge = Color.Parse("#3A4540");

    public static readonly IBrush InkFloorBrush = new SolidColorBrush(InkFloor);
    public static readonly IBrush TatamiBrush = new SolidColorBrush(Tatami);
    public static readonly IBrush BambooBrush = new SolidColorBrush(Bamboo);
    public static readonly IBrush BambooBrightBrush = new SolidColorBrush(BambooBright);
    public static readonly IBrush LacquerBrush = new SolidColorBrush(Lacquer);
    public static readonly IBrush GoldBrush = new SolidColorBrush(Gold);
    public static readonly IBrush WashiBrush = new SolidColorBrush(Washi);
    public static readonly IBrush PaneBrush = new SolidColorBrush(Pane);
    public static readonly IBrush PaneEdgeBrush = new SolidColorBrush(PaneEdge);
}
