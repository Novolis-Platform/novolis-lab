using Microsoft.Extensions.DependencyInjection;
using SpaceFleetSurveyTeam.Views;

namespace SpaceFleetSurveyTeam;

/// <summary>Shared DI registration for Space Fleet: Survey Team.</summary>
public static class SpaceFleetSurveyTeamServiceCollectionExtensions
{
    public static IServiceCollection AddSpaceFleetSurveyTeamCore(this IServiceCollection services)
    {
        services.AddTransient<FieldShellView>();
        return services;
    }
}
