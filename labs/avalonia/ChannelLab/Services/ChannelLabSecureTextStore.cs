using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Novolis.Avalonia.Mobile;
using Novolis.Avalonia.Mobile.Desktop;
using Novolis.Messaging.SecureText;
using Novolis.Security.SecureText;

namespace ChannelLab.Services;

/// <summary>Credential Manager-backed local persistence for ChannelLab secure-text material.</summary>
internal sealed class ChannelLabSecureTextStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ISecureTextKeyStore _identities;
    private readonly ISecureTokenStore _tokens;

    public ChannelLabSecureTextStore()
        : this(new WindowsCredentialTokenStore())
    {
    }

    internal ChannelLabSecureTextStore(ISecureTokenStore tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _identities = new TokenStoreKeyStore(tokens);
    }

    public async Task<SecureTextDeviceIdentity> LoadOrCreateIdentityAsync(string nick, CancellationToken cancellationToken)
    {
        var key = IdentityKey(nick);
        var identity = await _identities.LoadAsync(key, cancellationToken).ConfigureAwait(false);
        if (identity is not null)
            return identity;

        identity = SecureTextDeviceIdentity.Create();
        await _identities.StoreAsync(key, identity, cancellationToken).ConfigureAwait(false);
        return identity;
    }

    public async Task<SecureTextTrustedPeer?> LoadTrustedPeerAsync(
        string nick,
        string peerNick,
        CancellationToken cancellationToken)
    {
        var payload = await _tokens.GetAsync(TrustKey(nick, peerNick), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        var persisted = JsonSerializer.Deserialize<PersistedPeerTrust>(payload, JsonOptions)
                        ?? throw new InvalidDataException("The stored peer trust record is invalid.");
        return SecureTextTrustedPeer.FromFingerprint(persisted.DeviceId, persisted.Fingerprint);
    }

    public Task StoreTrustedPeerAsync(
        string nick,
        string peerNick,
        SecureTextTrustedPeer peer,
        CancellationToken cancellationToken) =>
        _tokens.SetAsync(
            TrustKey(nick, peerNick),
            JsonSerializer.Serialize(new PersistedPeerTrust(peer.DeviceId, peer.GetFingerprint()), JsonOptions),
            cancellationToken);

    public async Task<SecureTextConversationState> LoadConversationStateAsync(
        string nick,
        SecureTextDeviceId localDeviceId,
        SecureTextDeviceId peerDeviceId,
        CancellationToken cancellationToken)
    {
        var payload = await _tokens.GetAsync(StateKey(nick, localDeviceId, peerDeviceId), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(payload))
            return new SecureTextConversationState();

        var snapshot = JsonSerializer.Deserialize<SecureTextConversationStateSnapshot>(payload, JsonOptions)
                       ?? throw new InvalidDataException("The stored secure-text conversation state is invalid.");
        return new SecureTextConversationState(snapshot);
    }

    public Task StoreConversationStateAsync(
        string nick,
        SecureTextDeviceId localDeviceId,
        SecureTextDeviceId peerDeviceId,
        SecureTextConversationState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        return _tokens.SetAsync(
            StateKey(nick, localDeviceId, peerDeviceId),
            JsonSerializer.Serialize(state.Snapshot(), JsonOptions),
            cancellationToken);
    }

    public async Task<SecureTextConversationState> LoadGroupConversationStateAsync(
        string nick,
        SecureTextGroupId groupId,
        SecureTextDeviceId localDeviceId,
        SecureTextDeviceId peerDeviceId,
        CancellationToken cancellationToken)
    {
        var payload = await _tokens.GetAsync(GroupStateKey(nick, groupId, localDeviceId, peerDeviceId), cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(payload))
            return new SecureTextConversationState();

        var snapshot = JsonSerializer.Deserialize<SecureTextConversationStateSnapshot>(payload, JsonOptions)
                       ?? throw new InvalidDataException("The stored protected group conversation state is invalid.");
        return new SecureTextConversationState(snapshot);
    }

    public Task StoreGroupConversationStateAsync(
        string nick,
        SecureTextGroupId groupId,
        SecureTextDeviceId localDeviceId,
        SecureTextDeviceId peerDeviceId,
        SecureTextConversationState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        return _tokens.SetAsync(
            GroupStateKey(nick, groupId, localDeviceId, peerDeviceId),
            JsonSerializer.Serialize(state.Snapshot(), JsonOptions),
            cancellationToken);
    }

    public Task StoreGroupApprovalAsync(
        string nick,
        SecureTextGroupId groupId,
        IReadOnlyCollection<SecureTextDeviceId> memberDeviceIds,
        CancellationToken cancellationToken) =>
        _tokens.SetAsync(
            GroupApprovalKey(nick, groupId, memberDeviceIds),
            "approved",
            cancellationToken);

    public async Task<bool> HasGroupApprovalAsync(
        string nick,
        SecureTextGroupId groupId,
        IReadOnlyCollection<SecureTextDeviceId> memberDeviceIds,
        CancellationToken cancellationToken)
    {
        var payload = await _tokens.GetAsync(GroupApprovalKey(nick, groupId, memberDeviceIds), cancellationToken)
            .ConfigureAwait(false);
        return string.Equals(payload, "approved", StringComparison.Ordinal);
    }

    private static string IdentityKey(string nick) => $"channellab.securetext.identity.{NormalizeNick(nick)}";

    private static string TrustKey(string nick, string peerNick) =>
        $"channellab.securetext.trust.{NormalizeNick(nick)}.{NormalizeNick(peerNick)}";

    private static string StateKey(string nick, SecureTextDeviceId localDeviceId, SecureTextDeviceId peerDeviceId) =>
        $"channellab.securetext.state.{NormalizeNick(nick)}.{localDeviceId.Value:N}.{peerDeviceId.Value:N}";

    private static string GroupStateKey(
        string nick,
        SecureTextGroupId groupId,
        SecureTextDeviceId localDeviceId,
        SecureTextDeviceId peerDeviceId) =>
        $"channellab.securetext.group-state.{NormalizeNick(nick)}.{groupId.Value:N}.{localDeviceId.Value:N}.{peerDeviceId.Value:N}";

    private static string GroupApprovalKey(
        string nick,
        SecureTextGroupId groupId,
        IReadOnlyCollection<SecureTextDeviceId> memberDeviceIds)
    {
        if (groupId.Value == Guid.Empty || memberDeviceIds.Count < 2)
            throw new ArgumentException("A group approval requires a non-empty id and at least two devices.");

        var roster = string.Join(
            ".",
            memberDeviceIds
                .Select(deviceId => deviceId.Value.ToString("N"))
                .OrderBy(value => value, StringComparer.Ordinal));
        var rosterDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(roster)));
        return $"channellab.securetext.group-approval.{NormalizeNick(nick)}.{groupId.Value:N}.{rosterDigest}";
    }

    private static string NormalizeNick(string nick)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nick);
        return nick.Trim().ToLowerInvariant();
    }

    private sealed record PersistedPeerTrust(Guid DeviceId, string Fingerprint);

    private sealed class TokenStoreKeyStore(ISecureTokenStore tokens) : ISecureTextKeyStore
    {
        public async Task<SecureTextDeviceIdentity?> LoadAsync(string key, CancellationToken cancellationToken = default)
        {
            var payload = await tokens.GetAsync(key, cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(payload)
                ? null
                : SecureTextDeviceIdentityCodec.Deserialize(Convert.FromBase64String(payload));
        }

        public Task StoreAsync(string key, SecureTextDeviceIdentity identity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(identity);
            var payload = Convert.ToBase64String(SecureTextDeviceIdentityCodec.Serialize(identity));
            return tokens.SetAsync(key, payload, cancellationToken);
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            tokens.RemoveAsync(key, cancellationToken);
    }
}
