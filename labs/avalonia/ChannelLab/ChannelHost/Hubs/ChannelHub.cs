using ChannelHost.Contracts;
using ChannelHost.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Novolis.Game.Identity.AspNetCore;
using Novolis.Messaging.SecureText;
using Novolis.Security.SecureText;

namespace ChannelHost.Hubs;

[Authorize]
public sealed class ChannelHub : Hub
{
    readonly ChannelDirectory _directory;
    readonly SqliteMessageStore _store;
    readonly ILogger<ChannelHub> _logger;

    public ChannelHub(ChannelDirectory directory, SqliteMessageStore store, ILogger<ChannelHub> logger)
    {
        _directory = directory;
        _store = store;
        _logger = logger;
    }

    public async Task Join(string channel)
    {
        channel = NormalizeChannel(channel);
        if (!_directory.IsKnownChannel(channel))
            throw new HubException($"Unknown channel '{channel}'.");

        if (!Context.User!.TryGetPlayerRef(out var player))
            throw new HubException("Missing player claim.");

        var nick = ResolveNick();
        var prior = _directory.FindChannelForConnection(Context.ConnectionId);
        if (prior is not null && !string.Equals(prior, channel, StringComparison.OrdinalIgnoreCase))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, prior).ConfigureAwait(false);
            var leftRoster = _directory.Part(prior, Context.ConnectionId);
            if (leftRoster is not null)
                await Clients.Group(prior).SendAsync("Roster", new RosterDto(prior, leftRoster)).ConfigureAwait(false);
        }

        var roster = _directory.Join(channel, player, nick, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, channel).ConfigureAwait(false);

        await Clients.Group(channel).SendAsync("Roster", new RosterDto(channel, roster)).ConfigureAwait(false);
        _logger.LogInformation("{Nick} joined {Channel}", nick, channel);
    }

    public async Task Part(string channel)
    {
        channel = NormalizeChannel(channel);
        var nick = ResolveNick();
        var wasVideo = _directory.TryPartVideo(channel, Context.ConnectionId);
        var roster = _directory.Part(channel, Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel).ConfigureAwait(false);
        if (wasVideo)
        {
            var envelope = new SignalEnvelope(channel, nick, "video-part", string.Empty);
            await Clients.Group(channel).SendAsync("Signal", envelope).ConfigureAwait(false);
        }
        if (roster is not null)
            await Clients.Group(channel).SendAsync("Roster", new RosterDto(channel, roster)).ConfigureAwait(false);
    }

    public Task RegisterDevice(string channel, DeviceBundleDto bundle)
    {
        channel = NormalizeChannel(channel);
        if (!_directory.IsKnownChannel(channel))
            throw new HubException($"Unknown channel '{channel}'.");
        if (!Context.User!.TryGetPlayerRef(out _))
            throw new HubException("Missing player claim.");

        var current = _directory.FindChannelForConnection(Context.ConnectionId);
        if (!string.Equals(current, channel, StringComparison.OrdinalIgnoreCase))
            throw new HubException("Join the channel before registering a device.");

        var publicBundle = ToPublicBundle(bundle);
        if (!publicBundle.Verify())
            throw new HubException("The device bundle signature or validity window is invalid.");
        if (!_directory.TryRegisterDevice(channel, ResolveNick(), Context.ConnectionId, publicBundle))
            throw new HubException("The device cannot be registered for this connection.");

        _logger.LogInformation("Registered secure-text device {DeviceId} for {Nick}", publicBundle.DeviceId, ResolveNick());
        return Task.CompletedTask;
    }

    public Task<DeviceBundleDto?> GetDeviceBundle(string channel, string nick)
    {
        channel = NormalizeChannel(channel);
        if (!_directory.IsKnownChannel(channel))
            throw new HubException($"Unknown channel '{channel}'.");
        var current = _directory.FindChannelForConnection(Context.ConnectionId);
        if (!string.Equals(current, channel, StringComparison.OrdinalIgnoreCase))
            throw new HubException("Join the channel before resolving a peer.");

        var bundle = _directory.TryGetDeviceBundle(channel, nick);
        return Task.FromResult(bundle is null ? null : ToDto(bundle));
    }

    public async Task<IReadOnlyList<SecureTextRelayEnvelopeDto>> GetSecureHistory(string channel)
    {
        channel = NormalizeChannel(channel);
        var current = _directory.FindChannelForConnection(Context.ConnectionId);
        if (!string.Equals(current, channel, StringComparison.OrdinalIgnoreCase))
            throw new HubException("Join the channel before reading protected history.");

        return await _store.GetRecentAsync(channel, ResolveNick()).ConfigureAwait(false);
    }

    public async Task CreateSecureTextGroup(
        string channel,
        Guid groupId,
        string name,
        IReadOnlyList<SecureTextGroupMemberDto> members)
    {
        channel = NormalizeChannel(channel);
        var current = _directory.FindChannelForConnection(Context.ConnectionId);
        if (!string.Equals(current, channel, StringComparison.OrdinalIgnoreCase))
            throw new HubException("Join the channel before creating a protected group.");
        if (members is null)
            throw new HubException("The protected group membership is required.");

        var nick = ResolveNick();
        var initiator = members.SingleOrDefault(member =>
            string.Equals(member.Nick, nick, StringComparison.OrdinalIgnoreCase));
        if (initiator is null
            || !_directory.TryCreateSecureTextGroup(
                channel,
                groupId,
                name,
                nick,
                initiator.DeviceId,
                members,
                out var group))
        {
            throw new HubException("The protected group membership is invalid.");
        }

        await BroadcastSecureTextGroupAsync(channel, group!).ConfigureAwait(false);
    }

    public async Task ApproveSecureTextGroup(string channel, Guid groupId)
    {
        channel = NormalizeChannel(channel);
        var current = _directory.FindChannelForConnection(Context.ConnectionId);
        if (!string.Equals(current, channel, StringComparison.OrdinalIgnoreCase))
            throw new HubException("Join the channel before approving a protected group.");

        var nick = ResolveNick();
        var bundle = _directory.TryGetDeviceBundle(channel, nick);
        if (bundle is null
            || !_directory.TryApproveSecureTextGroup(channel, groupId, nick, bundle.DeviceId, out var group))
        {
            throw new HubException("The protected group approval is invalid.");
        }

        await BroadcastSecureTextGroupAsync(channel, group!).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<SecureTextGroupDto>> GetSecureTextGroups(string channel)
    {
        channel = NormalizeChannel(channel);
        var current = _directory.FindChannelForConnection(Context.ConnectionId);
        if (!string.Equals(current, channel, StringComparison.OrdinalIgnoreCase))
            throw new HubException("Join the channel before reading protected groups.");

        var bundle = _directory.TryGetDeviceBundle(channel, ResolveNick());
        if (bundle is null)
            throw new HubException("Register a secure-text device before reading protected groups.");

        return Task.FromResult(_directory.GetSecureTextGroupsForDevice(channel, bundle.DeviceId));
    }

    public async Task<IReadOnlyList<SecureTextGroupRelayEnvelopeDto>> GetSecureTextGroupHistory(
        string channel,
        Guid groupId)
    {
        channel = NormalizeChannel(channel);
        var current = _directory.FindChannelForConnection(Context.ConnectionId);
        if (!string.Equals(current, channel, StringComparison.OrdinalIgnoreCase))
            throw new HubException("Join the channel before reading protected group history.");

        var nick = ResolveNick();
        var bundle = _directory.TryGetDeviceBundle(channel, nick);
        if (bundle is null || !_directory.IsSecureTextGroupMember(channel, groupId, nick, bundle.DeviceId))
            throw new HubException("The device is not a protected group member.");

        return await _store.GetRecentGroupAsync(channel, groupId, nick).ConfigureAwait(false);
    }

    public async Task SendSecureText(string channel, string toNick, byte[] envelope)
    {
        channel = NormalizeChannel(channel);
        if (!_directory.IsKnownChannel(channel))
            throw new HubException($"Unknown channel '{channel}'.");
        var current = _directory.FindChannelForConnection(Context.ConnectionId);
        if (!string.Equals(current, channel, StringComparison.OrdinalIgnoreCase))
            throw new HubException("Join the channel before sending protected text.");
        if (string.IsNullOrWhiteSpace(toNick))
            throw new HubException("A recipient is required.");
        if (envelope is null || envelope.Length > SecureTextEnvelopeCodec.GetMaximumSerializedBytes())
            throw new HubException("The protected envelope length is invalid.");

        SecureTextEnvelope protectedEnvelope;
        try
        {
            protectedEnvelope = SecureTextEnvelopeCodec.Deserialize(envelope);
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or NotSupportedException or ArgumentException)
        {
            throw new HubException("The protected envelope is invalid.");
        }

        var fromNick = ResolveNick();
        if (!_directory.IsRegisteredDevice(channel, fromNick, protectedEnvelope.Header.SenderDeviceId.Value))
            throw new HubException("The protected envelope sender device is not registered for this connection.");
        var targetBundle = _directory.TryGetDeviceBundle(channel, toNick);
        if (targetBundle is null || targetBundle.DeviceId != protectedEnvelope.Header.RecipientDeviceId.Value)
            throw new HubException("The protected envelope recipient does not match the selected peer.");
        var targetConnection = _directory.FindConnectionForDevice(channel, targetBundle.DeviceId);
        if (targetConnection is null)
            throw new HubException("The selected peer is not connected.");

        var relay = new SecureTextRelayEnvelopeDto(channel, fromNick, toNick.Trim(), envelope);
        await _store.AppendAsync(relay).ConfigureAwait(false);
        await Clients.Client(targetConnection).SendAsync("SecureText", relay).ConfigureAwait(false);
    }

    public async Task SendSecureTextGroup(
        string channel,
        Guid groupId,
        IReadOnlyList<SecureTextGroupEnvelopeInputDto> envelopes)
    {
        channel = NormalizeChannel(channel);
        var current = _directory.FindChannelForConnection(Context.ConnectionId);
        if (!string.Equals(current, channel, StringComparison.OrdinalIgnoreCase))
            throw new HubException("Join the channel before sending protected group text.");

        var group = _directory.TryGetSecureTextGroup(channel, groupId);
        if (group is null || group.ApprovedDeviceIds.Count != group.Members.Count)
            throw new HubException("Every group device must explicitly approve this membership before messaging.");

        var fromNick = ResolveNick();
        var senderBundle = _directory.TryGetDeviceBundle(channel, fromNick);
        if (senderBundle is null || !_directory.IsSecureTextGroupMember(channel, groupId, fromNick, senderBundle.DeviceId))
            throw new HubException("The sender device is not an active protected group member.");

        var expectedRecipients = group.Members
            .Where(member => member.DeviceId != senderBundle.DeviceId)
            .OrderBy(member => member.DeviceId)
            .ToArray();
        if (envelopes is null
            || envelopes.Count != expectedRecipients.Length
            || envelopes.Select(envelope => envelope.RecipientDeviceId).Distinct().Count() != envelopes.Count)
        {
            throw new HubException("A separate protected envelope is required for every group recipient.");
        }

        var sent = new List<(SecureTextGroupRelayEnvelopeDto Relay, string ConnectionId)>();
        foreach (var expected in expectedRecipients)
        {
            var input = envelopes.SingleOrDefault(envelope => envelope.RecipientDeviceId == expected.DeviceId);
            if (input is null || input.Envelope is null || input.Envelope.Length > SecureTextEnvelopeCodec.GetMaximumSerializedBytes())
                throw new HubException("A protected group envelope is invalid.");

            SecureTextEnvelope protectedEnvelope;
            try
            {
                protectedEnvelope = SecureTextEnvelopeCodec.Deserialize(input.Envelope);
            }
            catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or NotSupportedException or ArgumentException)
            {
                throw new HubException("A protected group envelope is invalid.");
            }

            var expectedConversation = SecureTextConversationId.DeriveForGroupMember(
                SecureTextGroupId.FromGuid(groupId),
                SecureTextDeviceId.FromGuid(senderBundle.DeviceId),
                SecureTextDeviceId.FromGuid(expected.DeviceId));
            if (protectedEnvelope.Header.SenderDeviceId.Value != senderBundle.DeviceId
                || protectedEnvelope.Header.RecipientDeviceId.Value != expected.DeviceId
                || protectedEnvelope.Header.ConversationId != expectedConversation)
            {
                throw new HubException("A protected group envelope does not match the approved membership.");
            }

            var targetConnection = _directory.FindConnectionForDevice(channel, expected.DeviceId);
            if (targetConnection is null)
                throw new HubException("Every protected group member must be connected.");

            sent.Add((
                new SecureTextGroupRelayEnvelopeDto(channel, groupId, group.Name, fromNick, expected.Nick, input.Envelope),
                targetConnection));
        }

        foreach (var entry in sent)
            await _store.AppendGroupAsync(entry.Relay).ConfigureAwait(false);
        foreach (var entry in sent)
            await Clients.Client(entry.ConnectionId).SendAsync("SecureTextGroup", entry.Relay).ConfigureAwait(false);
    }

    /// <summary>
    /// Relays RTC signaling. Kinds: video-join, video-part, offer, answer, ice.
    /// video-join is rejected when the channel already has 4 video participants.
    /// </summary>
    public async Task Signal(string channel, string kind, string payload, string? toNick = null)
    {
        channel = NormalizeChannel(channel);
        if (!_directory.IsKnownChannel(channel))
            throw new HubException($"Unknown channel '{channel}'.");

        var current = _directory.FindChannelForConnection(Context.ConnectionId);
        if (!string.Equals(current, channel, StringComparison.OrdinalIgnoreCase))
            throw new HubException("Join the channel before signaling.");

        kind = (kind ?? string.Empty).Trim().ToLowerInvariant();
        if (kind.Length == 0)
            throw new HubException("Signal kind required.");

        payload ??= string.Empty;
        toNick = string.IsNullOrWhiteSpace(toNick) ? null : toNick.Trim();
        var fromNick = ResolveNick();

        switch (kind)
        {
            case "video-join":
                if (!_directory.TryJoinVideo(channel, Context.ConnectionId))
                    throw new HubException($"Video mesh full (max {ChannelDirectory.MaxVideoParticipants}).");
                break;
            case "video-part":
                _directory.TryPartVideo(channel, Context.ConnectionId);
                break;
            case "offer":
            case "answer":
            case "ice":
                break;
            default:
                throw new HubException($"Unknown signal kind '{kind}'.");
        }

        var envelope = new SignalEnvelope(channel, fromNick, kind, payload, toNick);
        if (toNick is not null)
        {
            var targetConnection = _directory.FindConnectionForNick(channel, toNick);
            if (targetConnection is null)
                throw new HubException($"Unknown nick '{toNick}'.");
            await Clients.Client(targetConnection).SendAsync("Signal", envelope).ConfigureAwait(false);
        }
        else
        {
            await Clients.OthersInGroup(channel).SendAsync("Signal", envelope).ConfigureAwait(false);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var channel = _directory.FindChannelForConnection(Context.ConnectionId);
        var nick = _directory.FindNick(Context.ConnectionId);
        if (channel is not null && nick is not null && _directory.TryPartVideo(channel, Context.ConnectionId))
        {
            var envelope = new SignalEnvelope(channel, nick, "video-part", string.Empty);
            await Clients.OthersInGroup(channel).SendAsync("Signal", envelope).ConfigureAwait(false);
        }

        var roster = _directory.PartAll(Context.ConnectionId);
        if (channel is not null && roster is not null)
            await Clients.Group(channel).SendAsync("Roster", new RosterDto(channel, roster)).ConfigureAwait(false);
        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    string ResolveNick()
    {
        var nick = Context.User?.FindFirst(TokenService.NickClaim)?.Value
                   ?? Context.User?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(nick))
            throw new HubException("Missing nick claim.");
        return nick;
    }

    async Task BroadcastSecureTextGroupAsync(string channel, SecureTextGroupDto group)
    {
        foreach (var member in group.Members)
        {
            var connectionId = _directory.FindConnectionForDevice(channel, member.DeviceId);
            if (connectionId is not null)
                await Clients.Client(connectionId).SendAsync("SecureTextGroupMembership", group).ConfigureAwait(false);
        }
    }

    static string NormalizeChannel(string channel)
    {
        channel = (channel ?? string.Empty).Trim();
        if (channel.Length == 0)
            throw new HubException("Channel required.");
        if (!channel.StartsWith('#'))
            channel = "#" + channel;
        return channel;
    }

    static SecureTextPublicBundle ToPublicBundle(DeviceBundleDto bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return new SecureTextPublicBundle(
            bundle.ProtocolVersion,
            bundle.DeviceId,
            bundle.SigningPublicKey,
            bundle.AgreementPublicKey,
            bundle.IssuedAtUtc,
            bundle.ExpiresAtUtc,
            bundle.Signature);
    }

    static DeviceBundleDto ToDto(SecureTextPublicBundle bundle) =>
        new(
            bundle.ProtocolVersion,
            bundle.DeviceId,
            bundle.ExportSigningPublicKey(),
            bundle.ExportAgreementPublicKey(),
            bundle.IssuedAtUtc,
            bundle.ExpiresAtUtc,
            bundle.ExportSignature());
}
