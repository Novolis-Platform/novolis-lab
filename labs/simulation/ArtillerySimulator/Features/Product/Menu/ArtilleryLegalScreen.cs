using Novolis.Game.MenuFlows;

namespace ArtillerySimulator;

internal sealed class ArtilleryLegalScreen : IGameScreen
{
    public string ScreenId => ArtilleryScreenIds.Legal;

    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
