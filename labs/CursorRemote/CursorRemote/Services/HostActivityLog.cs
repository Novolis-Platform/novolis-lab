namespace CursorRemote.Services;

public readonly record struct HostLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Message);

/// <summary>
/// Ring-buffer activity log for the Windows host UI (and export).
/// </summary>
public sealed class HostActivityLog
{
    public const int DefaultCapacity = 2_000;

    private readonly object _gate = new();
    private readonly List<HostLogEntry> _entries = new(DefaultCapacity);
    private readonly int _capacity;

    public HostActivityLog(int capacity = DefaultCapacity)
    {
        _capacity = Math.Max(100, capacity);
    }

    public event EventHandler? Changed;

    public int Count
    {
        get
        {
            lock (_gate)
                return _entries.Count;
        }
    }

    public void Info(string message) => Append("info", message);

    public void Warn(string message) => Append("warn", message);

    public void Error(string message) => Append("error", message);

    public void Append(string level, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var entry = new HostLogEntry(
            DateTimeOffset.Now,
            string.IsNullOrWhiteSpace(level) ? "info" : level.Trim().ToLowerInvariant(),
            message.Trim());

        lock (_gate)
        {
            if (_entries.Count >= _capacity)
                _entries.RemoveRange(0, _entries.Count - _capacity + 1);
            _entries.Add(entry);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<HostLogEntry> Snapshot()
    {
        lock (_gate)
            return _entries.ToArray();
    }

    public string ExportText()
    {
        lock (_gate)
        {
            return string.Join(
                Environment.NewLine,
                _entries.Select(FormatEntry));
        }
    }

    public void Clear()
    {
        lock (_gate)
            _entries.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public static string FormatEntry(HostLogEntry entry) =>
        $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}  {entry.Level,-5}  {entry.Message}";
}
