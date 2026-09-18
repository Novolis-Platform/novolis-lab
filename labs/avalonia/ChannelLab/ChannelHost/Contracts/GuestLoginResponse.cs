namespace ChannelHost.Contracts;

public sealed record GuestLoginResponse(string AccessToken, string Nick, Guid PlayerId, DateTimeOffset ExpiresAtUtc);
