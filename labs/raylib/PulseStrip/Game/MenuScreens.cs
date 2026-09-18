namespace PulseStrip.Game;

using Novolis.Game.MenuFlows;

internal sealed class TitleScreen : IGameScreen
{
    public string ScreenId => "title";
    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

internal sealed class CircuitScreen : IGameScreen
{
    public string ScreenId => "circuit";
    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

internal sealed class RaceScreen : IGameScreen
{
    public string ScreenId => "race";
    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

internal sealed class ResultsScreen : IGameScreen
{
    public string ScreenId => "results";
    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
