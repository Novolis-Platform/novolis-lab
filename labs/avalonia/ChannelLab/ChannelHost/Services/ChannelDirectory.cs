using System.Collections.Concurrent;
using ChannelHost.Contracts;
using Novolis.Game.Identity.Abstractions;
using Novolis.Security.SecureText;

namespace ChannelHost.Services;

public sealed class ChannelDirectory
{
    public const string Lobby = "#lobby";
    public const int MaxVideoParticipants = 4;
    public const int MaxSecureTextGroupMembers = 8;

    readonly ConcurrentDictionary<string, ChannelState> _channels = new(StringComparer.OrdinalIgnoreCase);

    public ChannelDirectory()
    {
        _channels[Lobby] = new ChannelState(Lobby);
    }

    public bool IsKnownChannel(string channel) =>
        string.Equals(channel, Lobby, StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> Join(string channel, PlayerRef player, string nick, string connectionId)
    {
        var state = GetOrThrow(channel);
        lock (state.Gate)
        {
            state.Members[connectionId] = new Member(player, nick);
            return RosterNicks(state);
        }
    }

    public IReadOnlyList<string>? Part(string channel, string connectionId)
    {
        if (!_channels.TryGetValue(channel, out var state))
            return null;

        lock (state.Gate)
        {
            state.Members.Remove(connectionId);
            state.VideoMembers.Remove(connectionId);
            return RosterNicks(state);
        }
    }

    public IReadOnlyList<string>? PartAll(string connectionId)
    {
        string? channel = null;
        IReadOnlyList<string>? roster = null;
        foreach (var pair in _channels)
        {
            lock (pair.Value.Gate)
            {
                if (pair.Value.Members.Remove(connectionId, out _))
                {
                    pair.Value.VideoMembers.Remove(connectionId);
                    channel = pair.Key;
                    roster = RosterNicks(pair.Value);
                }
            }
        }

        return channel is null ? null : roster;
    }

    public string? FindChannelForConnection(string connectionId)
    {
        foreach (var pair in _channels)
        {
            lock (pair.Value.Gate)
            {
                if (pair.Value.Members.ContainsKey(connectionId))
                    return pair.Key;
            }
        }

        return null;
    }

    public string? FindNick(string connectionId)
    {
        foreach (var pair in _channels)
        {
            lock (pair.Value.Gate)
            {
                if (pair.Value.Members.TryGetValue(connectionId, out var member))
                    return member.Nick;
            }
        }

        return null;
    }

    /// <summary>Registers a video participant. Returns false if the mesh is full (max 4).</summary>
    public bool TryJoinVideo(string channel, string connectionId)
    {
        var state = GetOrThrow(channel);
        lock (state.Gate)
        {
            if (!state.Members.ContainsKey(connectionId))
                return false;
            if (state.VideoMembers.Contains(connectionId))
                return true;
            if (state.VideoMembers.Count >= MaxVideoParticipants)
                return false;
            state.VideoMembers.Add(connectionId);
            return true;
        }
    }

    /// <summary>Removes video membership. Returns true if the connection was in the video mesh.</summary>
    public bool TryPartVideo(string channel, string connectionId)
    {
        if (!_channels.TryGetValue(channel, out var state))
            return false;
        lock (state.Gate)
            return state.VideoMembers.Remove(connectionId);
    }

    public string? FindConnectionForNick(string channel, string nick)
    {
        var state = GetOrThrow(channel);
        lock (state.Gate)
        {
            foreach (var pair in state.Members)
            {
                if (string.Equals(pair.Value.Nick, nick, StringComparison.OrdinalIgnoreCase))
                    return pair.Key;
            }
        }

        return null;
    }

    public bool TryRegisterDevice(
        string channel,
        string nick,
        string connectionId,
        SecureTextPublicBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var state = GetOrThrow(channel);
        lock (state.Gate)
        {
            if (!state.Members.TryGetValue(connectionId, out var member)
                || !string.Equals(member.Nick, nick, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (state.Devices.TryGetValue(bundle.DeviceId, out var existing)
                && !string.Equals(existing.Nick, nick, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var replacedDeviceIds = state.Devices
                .Where(pair => string.Equals(pair.Value.Nick, nick, StringComparison.OrdinalIgnoreCase)
                               && pair.Key != bundle.DeviceId)
                .Select(pair => pair.Key)
                .ToArray();
            foreach (var deviceId in replacedDeviceIds)
                state.Devices.Remove(deviceId);

            state.Devices[bundle.DeviceId] = new Device(nick, bundle);
            return true;
        }
    }

    public SecureTextPublicBundle? TryGetDeviceBundle(string channel, string nick)
    {
        var state = GetOrThrow(channel);
        lock (state.Gate)
        {
            foreach (var device in state.Devices.Values)
            {
                if (string.Equals(device.Nick, nick, StringComparison.OrdinalIgnoreCase))
                    return device.Bundle;
            }

            return null;
        }
    }

    public bool IsRegisteredDevice(string channel, string nick, Guid deviceId)
    {
        var state = GetOrThrow(channel);
        lock (state.Gate)
        {
            return state.Devices.TryGetValue(deviceId, out var device)
                   && string.Equals(device.Nick, nick, StringComparison.OrdinalIgnoreCase);
        }
    }

    public string? FindConnectionForDevice(string channel, Guid deviceId)
    {
        var state = GetOrThrow(channel);
        lock (state.Gate)
        {
            if (!state.Devices.TryGetValue(deviceId, out var device))
                return null;

            return state.Members
                .FirstOrDefault(pair => string.Equals(pair.Value.Nick, device.Nick, StringComparison.OrdinalIgnoreCase))
                .Key;
        }
    }

    public bool TryCreateSecureTextGroup(
        string channel,
        Guid groupId,
        string name,
        string initiatorNick,
        Guid initiatorDeviceId,
        IReadOnlyList<SecureTextGroupMemberDto> members,
        out SecureTextGroupDto? group)
    {
        group = null;
        if (groupId == Guid.Empty
            || string.IsNullOrWhiteSpace(name)
            || members.Count is < 2 or > MaxSecureTextGroupMembers)
        {
            return false;
        }

        var state = GetOrThrow(channel);
        lock (state.Gate)
        {
            if (state.SecureTextGroups.ContainsKey(groupId)
                || !IsRegisteredDevice(state, initiatorNick, initiatorDeviceId)
                || !MembersAreValid(state, members)
                || !members.Any(member => member.DeviceId == initiatorDeviceId
                                          && string.Equals(member.Nick, initiatorNick, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var groupState = new SecureTextGroupState(
                groupId,
                name.Trim(),
                initiatorNick,
                members.Select(member => new SecureTextGroupMember(member.Nick, member.DeviceId)).ToArray());
            groupState.ApprovedDeviceIds.Add(initiatorDeviceId);
            state.SecureTextGroups[groupId] = groupState;
            group = ToDto(groupState);
            return true;
        }
    }

    public bool TryApproveSecureTextGroup(
        string channel,
        Guid groupId,
        string nick,
        Guid deviceId,
        out SecureTextGroupDto? group)
    {
        group = null;
        var state = GetOrThrow(channel);
        lock (state.Gate)
        {
            if (!state.SecureTextGroups.TryGetValue(groupId, out var groupState)
                || !IsRegisteredDevice(state, nick, deviceId)
                || !groupState.Members.Any(member => member.DeviceId == deviceId
                                                     && string.Equals(member.Nick, nick, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            groupState.ApprovedDeviceIds.Add(deviceId);
            group = ToDto(groupState);
            return true;
        }
    }

    public SecureTextGroupDto? TryGetSecureTextGroup(string channel, Guid groupId)
    {
        var state = GetOrThrow(channel);
        lock (state.Gate)
            return state.SecureTextGroups.TryGetValue(groupId, out var group) ? ToDto(group) : null;
    }

    public IReadOnlyList<SecureTextGroupDto> GetSecureTextGroupsForDevice(string channel, Guid deviceId)
    {
        var state = GetOrThrow(channel);
        lock (state.Gate)
        {
            return state.SecureTextGroups.Values
                .Where(group => group.Members.Any(member => member.DeviceId == deviceId))
                .Select(ToDto)
                .ToArray();
        }
    }

    public bool IsSecureTextGroupMember(string channel, Guid groupId, string nick, Guid deviceId)
    {
        var state = GetOrThrow(channel);
        lock (state.Gate)
        {
            return state.SecureTextGroups.TryGetValue(groupId, out var group)
                   && group.Members.Any(member => member.DeviceId == deviceId
                                                   && string.Equals(member.Nick, nick, StringComparison.OrdinalIgnoreCase));
        }
    }

    ChannelState GetOrThrow(string channel)
    {
        if (!_channels.TryGetValue(channel, out var state))
            throw new InvalidOperationException($"Unknown channel '{channel}'.");
        return state;
    }

    static IReadOnlyList<string> RosterNicks(ChannelState state) =>
        state.Members.Values
            .Select(m => m.Nick)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

    static bool IsRegisteredDevice(ChannelState state, string nick, Guid deviceId) =>
        state.Devices.TryGetValue(deviceId, out var device)
        && string.Equals(device.Nick, nick, StringComparison.OrdinalIgnoreCase);

    static bool MembersAreValid(ChannelState state, IReadOnlyList<SecureTextGroupMemberDto> members)
    {
        if (members.Select(member => member.DeviceId).Distinct().Count() != members.Count
            || members.Select(member => member.Nick).Distinct(StringComparer.OrdinalIgnoreCase).Count() != members.Count)
        {
            return false;
        }

        foreach (var member in members)
        {
            if (string.IsNullOrWhiteSpace(member.Nick)
                || !IsRegisteredDevice(state, member.Nick, member.DeviceId)
                || state.Members.Values.All(connected => !string.Equals(connected.Nick, member.Nick, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return true;
    }

    static SecureTextGroupDto ToDto(SecureTextGroupState group) =>
        new(
            group.GroupId,
            group.Name,
            group.InitiatorNick,
            group.Members.Select(member => new SecureTextGroupMemberDto(member.Nick, member.DeviceId)).ToArray(),
            group.ApprovedDeviceIds.Order().ToArray());

    sealed class ChannelState(string name)
    {
        public string Name { get; } = name;
        public object Gate { get; } = new();
        public Dictionary<string, Member> Members { get; } = new(StringComparer.Ordinal);
        public HashSet<string> VideoMembers { get; } = new(StringComparer.Ordinal);
        public Dictionary<Guid, Device> Devices { get; } = [];
        public Dictionary<Guid, SecureTextGroupState> SecureTextGroups { get; } = [];
    }

    readonly record struct Member(PlayerRef Player, string Nick);
    readonly record struct Device(string Nick, SecureTextPublicBundle Bundle);
    readonly record struct SecureTextGroupMember(string Nick, Guid DeviceId);

    sealed class SecureTextGroupState(
        Guid groupId,
        string name,
        string initiatorNick,
        IReadOnlyList<SecureTextGroupMember> members)
    {
        public Guid GroupId { get; } = groupId;
        public string Name { get; } = name;
        public string InitiatorNick { get; } = initiatorNick;
        public IReadOnlyList<SecureTextGroupMember> Members { get; } = members;
        public HashSet<Guid> ApprovedDeviceIds { get; } = [];
    }
}
