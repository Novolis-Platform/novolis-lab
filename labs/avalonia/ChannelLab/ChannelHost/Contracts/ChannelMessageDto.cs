namespace ChannelHost.Contracts;

public sealed record ChannelMessageDto(string Channel, string Nick, string Body, DateTimeOffset At);
