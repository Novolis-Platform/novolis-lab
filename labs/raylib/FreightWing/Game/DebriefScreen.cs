using Novolis.Game.MenuFlows;

namespace FreightWing.Game;

internal sealed class DebriefScreen : IGameScreen
{
    public string ScreenId => "debrief";
    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
