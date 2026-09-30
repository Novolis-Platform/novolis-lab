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

file sealed class SmokePauseScreen : PauseScreenBase;
