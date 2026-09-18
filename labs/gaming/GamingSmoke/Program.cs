using Novolis.Game.Identity;
using Novolis.Game.Identity.Abstractions;
using Novolis.Game.MenuFlows;
using Novolis.Game.Multiplayer.Abstractions;

var directory = new InMemoryPlayerDirectory();
var player = PlayerRefFactory.CreateGuest(directory, "Guest-smoke");

var stack = new GameScreenStack();
await stack.PushAsync(new SmokeScreen("main"));
await stack.PushAsync(new SmokePauseScreen());

var lobby = new InMemoryLobbyState();
lobby.TryAddPlayer(new LobbyPlayerSlot(player, false));
lobby.TrySetReady(player, true);

Console.WriteLine($"Player={player} Screen={stack.Current?.ScreenId} LobbyPlayers={lobby.Players.Count}");

return 0;

file sealed class SmokeScreen(string id) : IGameScreen
{
    public string ScreenId => id;

    public ValueTask OnEnterAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask OnExitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

file sealed class SmokePauseScreen : PauseScreenBase;
