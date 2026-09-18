namespace FriendLab.Core;

/// <summary>In-process stand-in for the old Cosmos/Mongo user directory.</summary>
internal sealed class FriendHub
{
    readonly object _gate = new();
    readonly Dictionary<string, FriendProfile> _profiles = new(StringComparer.Ordinal);
    readonly List<Action> _listeners = [];

    public IReadOnlyList<FriendProfile> Snapshot()
    {
        lock (_gate)
            return _profiles.Values.Select(p => p.CloneSnapshot()).ToList();
    }

    public FriendProfile? GetLive(string id)
    {
        lock (_gate)
            return _profiles.TryGetValue(id, out var p) ? p : null;
    }

    public void ReplaceAll(IEnumerable<FriendProfile> profiles)
    {
        lock (_gate)
        {
            _profiles.Clear();
            foreach (var p in profiles)
                _profiles[p.Id] = p;
        }

        Notify();
    }

    public void Upsert(FriendProfile profile)
    {
        lock (_gate)
            _profiles[profile.Id] = profile;
        Notify();
    }

    public void PublishChanged() => Notify();

    public IDisposable Subscribe(Action onChanged)
    {
        lock (_gate)
            _listeners.Add(onChanged);
        return new Unsub(this, onChanged);
    }

    void Notify()
    {
        Action[] copy;
        lock (_gate)
            copy = _listeners.ToArray();
        foreach (var listener in copy)
        {
            try { listener(); }
            catch { /* UI listeners must not break peers */ }
        }
    }

    sealed class Unsub(FriendHub hub, Action listener) : IDisposable
    {
        public void Dispose()
        {
            lock (hub._gate)
                hub._listeners.Remove(listener);
        }
    }
}
