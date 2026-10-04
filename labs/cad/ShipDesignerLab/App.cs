using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.GraphicalProfile;

namespace ShipDesignerLab;

public sealed class App : Application
{
    private static AgentHost? s_agentHost;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
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
