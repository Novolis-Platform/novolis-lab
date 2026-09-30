namespace PulseStrip.Game;

using Novolis.Game.MenuFlows;

internal sealed class TitleScreen : IGameScreen
{
    public string ScreenId => "title";
    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
