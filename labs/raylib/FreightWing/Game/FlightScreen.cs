using Novolis.Game.MenuFlows;

namespace FreightWing.Game;

internal sealed class FlightScreen : IGameScreen
{
    public string ScreenId => "flight";
    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
