namespace PulseStrip.Game;

using Novolis.Game.MenuFlows;

internal sealed class RaceScreen : IGameScreen
{
    public string ScreenId => "race";
    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
