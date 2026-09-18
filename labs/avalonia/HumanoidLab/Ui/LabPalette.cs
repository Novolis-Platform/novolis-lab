using Avalonia.Media;

namespace HumanoidLab.Ui;

internal static class LabPalette
{
    public static readonly Color Navy = Color.Parse("#0B1C2C");
    public static readonly Color Teal = Color.Parse("#1A6B6B");
    public static readonly Color TealBright = Color.Parse("#2AA8A8");
    public static readonly Color Copper = Color.Parse("#C47A3A");
    public static readonly Color Amber = Color.Parse("#E0A04A");
    public static readonly Color Ink = Color.Parse("#D6E4EE");
    public static readonly Color Pane = Color.Parse("#122536");
    public static readonly Color PaneEdge = Color.Parse("#2A4558");

    public static readonly IBrush NavyBrush = new SolidColorBrush(Navy);
    public static readonly IBrush TealBrush = new SolidColorBrush(Teal);
    public static readonly IBrush TealBrightBrush = new SolidColorBrush(TealBright);
    public static readonly IBrush CopperBrush = new SolidColorBrush(Copper);
    public static readonly IBrush AmberBrush = new SolidColorBrush(Amber);
    public static readonly IBrush InkBrush = new SolidColorBrush(Ink);
    public static readonly IBrush PaneBrush = new SolidColorBrush(Pane);
    public static readonly IBrush PaneEdgeBrush = new SolidColorBrush(PaneEdge);
}
