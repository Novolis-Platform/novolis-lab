using Avalonia.Controls;

namespace FriendLab.Ui;

internal static class LayoutExtensions
{
    public static T WithDock<T>(this T control, Dock dock) where T : Control
    {
        DockPanel.SetDock(control, dock);
        return control;
    }
}
