using Novolis.Game.MenuFlows;

namespace FreightWing.Game;

internal sealed class MapScreen : IGameScreen
{
    public string ScreenId => "map";
    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

internal sealed class BriefingScreen : IGameScreen
{
    public string ScreenId => "briefing";
    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

internal sealed class FlightScreen : IGameScreen
{
    public string ScreenId => "flight";
    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

internal sealed class DebriefScreen : IGameScreen
{
    public string ScreenId => "debrief";
    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
