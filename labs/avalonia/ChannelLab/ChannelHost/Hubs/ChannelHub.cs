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
