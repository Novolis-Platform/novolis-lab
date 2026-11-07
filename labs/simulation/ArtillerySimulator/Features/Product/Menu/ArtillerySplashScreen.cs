using Novolis.Game.MenuFlows;

namespace ArtillerySimulator;

internal sealed class ArtillerySplashScreen : IGameScreen
{
    public string ScreenId => ArtilleryScreenIds.Splash;

    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
