using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Novolis.Avalonia.GraphicalProfile;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Agent;

namespace StudioChromeLab;

public class App : Application
{
    static AgentHost? s_agentHost;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        GraphicalProfile.Install(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = Program.ApplicationHost.Services.GetRequiredService<MainWindow>();
            desktop.MainWindow = window;
            s_agentHost = AgentHost.TryAttachFromEnvironment(window);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
