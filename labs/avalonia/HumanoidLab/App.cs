using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;
using HumanoidLab.Http;

namespace HumanoidLab;

public sealed class App : Application
{
    HumanoidLabHttpHost? _http;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = Program.ApplicationHost.Services.GetRequiredService<MainWindow>();
            desktop.MainWindow = window;
            _http = HumanoidLabHttpHost.Attach(window);
            desktop.Exit += async (_, _) =>
            {
                if (_http is not null)
                    await _http.DisposeAsync().ConfigureAwait(false);
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
