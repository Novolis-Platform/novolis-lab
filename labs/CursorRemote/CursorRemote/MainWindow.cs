using Avalonia.Controls;
using CursorRemote.Ui;

namespace CursorRemote;

public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = ProductBrand.Name;
        Width = 980;
        Height = 860;
        MinWidth = 720;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = RemotePalette.WindowBrush;
        FontFamily = RemotePalette.BodyFont;
        Foreground = RemotePalette.BodyBrush;
    }
}
