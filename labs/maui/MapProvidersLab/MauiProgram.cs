using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Novolis.Maui.GraphicalProfile;

namespace Novolis.Lab.MapProviders;

/// <summary>Bootstraps the Windows map provider comparison lab.</summary>
public static class MauiProgram
{
    /// <summary>Builds the MAUI application.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseGraphicalProfile();

        builder.Services
            .AddSingleton<HttpClient>(_ =>
            {
                var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(20),
                };
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Novolis.MapProvidersLab/1.0");
                return client;
            })
            .AddSingleton<MainPage>();

        return builder.Build();
    }
}
