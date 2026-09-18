namespace ChannelHost.Contracts;

public sealed record RosterDto(string Channel, IReadOnlyList<string> Nicks);
