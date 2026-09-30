using Novolis.Game.MenuFlows;

namespace FreightWing.Game;

internal sealed class BriefingScreen : IGameScreen
{
    public string ScreenId => "briefing";
    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
