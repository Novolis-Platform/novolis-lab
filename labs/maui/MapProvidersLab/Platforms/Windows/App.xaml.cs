using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using Microsoft.UI.Xaml;

namespace Novolis.Lab.MapProviders.WinUI;

/// <summary>Windows entry point for the MAUI map provider lab.</summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>Initializes the generated Windows application resources.</summary>
    public App() => InitializeComponent();

    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
