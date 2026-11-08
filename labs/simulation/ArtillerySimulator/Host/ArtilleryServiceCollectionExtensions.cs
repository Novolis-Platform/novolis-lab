using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novolis.Messaging.Channels;

namespace ArtillerySimulator;

/// <summary>DI module registration for the K9 DOG lab host.</summary>
internal static class ArtilleryServiceCollectionExtensions
{
    public static IServiceCollection AddArtillerySimulator(
        this IServiceCollection services,
        ArtilleryRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddArtilleryCommandChannel();
        services.AddArtilleryWorld();
        services.AddArtilleryPresentation();
        services.AddArtilleryShell();
        services.AddArtilleryPlayComposition();
        return services;
    }

    private static IServiceCollection AddArtilleryCommandChannel(this IServiceCollection services)
    {
        // Cross-thread agent + same-thread player discrete intents. Single reader = FrameDirector pump.
        services.AddChannel<BattleCommand>(
            ChannelType.Bounded,
            new ChannelSettings
            {
                BoundedCapacity = 64,
                BoundedFullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });
        return services;
    }

    private static IServiceCollection AddArtilleryWorld(this IServiceCollection services)
    {
        services.AddSingleton<TerrainWorld>();
        services.AddSingleton(_ => AtmosphereModel.CreateRegionalDefault());
        return services;
    }

    private static IServiceCollection AddArtilleryPresentation(this IServiceCollection services)
    {
        services.AddSingleton<ArtilleryCameras>();
        services.AddSingleton<ArtilleryParticles>();
        services.AddSingleton<FireControlUi>();
        services.AddSingleton<ShellOverlay>();
        services.AddSingleton<ProfileOverlay>();
        services.AddSingleton<ArtillerySceneRenderer>();
        services.AddSingleton<PlayerBattleInput>();
        services.AddSingleton<BattleFxSink>();
        services.AddSingleton<BattlePresentationBinder>();
        return services;
    }

    private static IServiceCollection AddArtilleryShell(this IServiceCollection services)
    {
        services.AddSingleton<ArtilleryMenuFlow>();
        services.AddSingleton<ArtilleryShellController>();
        return services;
    }

    private static IServiceCollection AddArtilleryPlayComposition(this IServiceCollection services)
    {
        // Binder before BattleWorld — world ctor calls Bind(...).
        services.AddSingleton<BattleWorld>();
        services.AddSingleton<IPlayQuery>(sp => sp.GetRequiredService<BattleWorld>());
        services.AddSingleton<IBatteryApplier>(sp => sp.GetRequiredService<BattleWorld>());
        services.AddSingleton<BattleCommandPump>();
        services.AddSingleton<ArtilleryLiveHost>();
        services.AddSingleton<FrameDirector>();
        services.AddSingleton<ArtilleryGameRunner>();
        return services;
    }
}
