using System.Collections.Concurrent;
using ChannelHost.Contracts;
using Novolis.Game.Identity.Abstractions;

namespace ChannelHost.Services;

public sealed class ChannelDirectory
{
    public const string Lobby = "#lobby";
    public const int MaxBodyLength = 2048;
    public const int RingCapacity = 100;
    public const int MaxVideoParticipants = 4;

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
                if (pair.Value.Members.Remove(connectionId))
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

    public void Remember(ChannelMessageDto message)
    {
        var state = GetOrThrow(message.Channel);
        lock (state.Gate)
        {
            state.Ring.AddLast(message);
            while (state.Ring.Count > RingCapacity)
                state.Ring.RemoveFirst();
        }
    }

    public IReadOnlyList<ChannelMessageDto> Recent(string channel)
    {
        var state = GetOrThrow(channel);
        lock (state.Gate)
            return state.Ring.ToList();
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

    sealed class ChannelState(string name)
    {
        public string Name { get; } = name;
        public object Gate { get; } = new();
        public Dictionary<string, Member> Members { get; } = new(StringComparer.Ordinal);
        public HashSet<string> VideoMembers { get; } = new(StringComparer.Ordinal);
        public LinkedList<ChannelMessageDto> Ring { get; } = new();
    }

    readonly record struct Member(PlayerRef Player, string Nick);
}
