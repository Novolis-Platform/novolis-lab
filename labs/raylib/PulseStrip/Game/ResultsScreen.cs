namespace PulseStrip.Game;

using Novolis.Game.MenuFlows;

internal sealed class ResultsScreen : IGameScreen
{
    public string ScreenId => "results";
    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
