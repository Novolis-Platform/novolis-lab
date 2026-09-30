using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Novolis.Avalonia.GraphicalProfile;

namespace EconomyBoard;

internal sealed class App : Application
{
  public override void Initialize()
  {
    Styles.Add(new FluentTheme());
    GraphicalProfile.Install(this);
  }

  public override void OnFrameworkInitializationCompleted()
  {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      desktop.MainWindow = new MainWindow();
    }

    base.OnFrameworkInitializationCompleted();
  }
}
