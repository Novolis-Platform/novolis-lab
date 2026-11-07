using Novolis.Game.MenuFlows;

namespace ArtillerySimulator;

internal sealed class ArtilleryLoadingScreen : IGameScreen
{
    public string ScreenId => ArtilleryScreenIds.Loading;

    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
