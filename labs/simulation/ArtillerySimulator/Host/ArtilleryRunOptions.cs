using Microsoft.Extensions.Logging;

namespace ArtillerySimulator;

/// <summary>Immutable launch configuration parsed from CLI / host configuration.</summary>
internal sealed class ArtilleryRunOptions
{
    public required IGameLevel Level { get; init; }
    public int? Seed { get; init; }
    public bool Headless { get; init; }
    public int? ProfileFrames { get; init; }
    public string? ProfileJsonPath { get; init; }
    public bool SkipShell => Headless || ProfileFrames is not null;
    public bool Verbose { get; init; }
    public LogLevel MinimumLogLevel => Verbose ? LogLevel.Debug : LogLevel.Information;
}
