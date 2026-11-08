namespace ArtillerySimulator;

/// <summary>Selectable tubes for Tab cycling. Kristiansand is a one-gun roster.</summary>
internal sealed class BatteryRoster
{
    private readonly BatteryMount[] _mounts;

    public BatteryRoster(IReadOnlyList<BatteryMount> mounts)
    {
        ArgumentNullException.ThrowIfNull(mounts);
        if (mounts.Count == 0)
            throw new ArgumentException("Battery needs at least one mount.", nameof(mounts));
        _mounts = mounts.ToArray();
    }

    public int Count => _mounts.Length;
    public int ActiveIndex { get; private set; }
    public BatteryMount Active => _mounts[ActiveIndex];
    public string ActiveLabel => Active.Label;
    public bool CanCycle => _mounts.Length > 1;

    public IReadOnlyList<BatteryMount> Mounts => _mounts;

    public bool TryCycle(int delta = 1)
    {
        if (!CanCycle)
            return false;
        var n = _mounts.Length;
        ActiveIndex = ((ActiveIndex + delta) % n + n) % n;
        return true;
    }

    public static BatteryRoster FromLevel(IGameLevel level) => new(level.ResolveBattery());
}
