namespace ChannelHost.Contracts;

/// <summary>Public device bundle exchanged through ChannelHost; it contains no private key material.</summary>
public sealed record DeviceBundleDto(
    int ProtocolVersion,
    Guid DeviceId,
    byte[] SigningPublicKey,
    byte[] AgreementPublicKey,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    byte[] Signature);
