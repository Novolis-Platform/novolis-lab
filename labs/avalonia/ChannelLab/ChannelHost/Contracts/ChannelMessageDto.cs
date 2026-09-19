namespace ChannelHost.Contracts;

/// <summary>Opaque direct envelope relayed without access to protected text.</summary>
public sealed record SecureTextRelayEnvelopeDto(string Channel, string FromNick, string ToNick, byte[] Envelope);
