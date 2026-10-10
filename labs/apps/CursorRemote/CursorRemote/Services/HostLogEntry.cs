namespace CursorRemote.Services;

public readonly record struct HostLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Message);
