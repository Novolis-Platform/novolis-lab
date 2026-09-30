using Novolis.Game.MenuFlows;

namespace FreightWing.Game;

internal sealed class MapScreen : IGameScreen
{
    public string ScreenId => "map";
    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
