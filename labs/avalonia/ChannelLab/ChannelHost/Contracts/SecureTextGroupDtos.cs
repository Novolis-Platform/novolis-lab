namespace ChannelHost.Contracts;

/// <summary>One device participating in an explicitly approved secure-text group.</summary>
public sealed record SecureTextGroupMemberDto(string Nick, Guid DeviceId);

/// <summary>Membership state relayed to every proposed or active group participant.</summary>
public sealed record SecureTextGroupDto(
    Guid GroupId,
    string Name,
    string InitiatorNick,
    IReadOnlyList<SecureTextGroupMemberDto> Members,
    IReadOnlyList<Guid> ApprovedDeviceIds);

/// <summary>One encrypted recipient copy of a pairwise-fan-out group text message.</summary>
public sealed record SecureTextGroupEnvelopeInputDto(Guid RecipientDeviceId, byte[] Envelope);

/// <summary>Opaque group message copy delivered to one recipient device.</summary>
public sealed record SecureTextGroupRelayEnvelopeDto(
    string Channel,
    Guid GroupId,
    string GroupName,
    string FromNick,
    string ToNick,
    byte[] Envelope);
