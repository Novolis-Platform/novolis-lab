using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CoverageStudio.Models;

internal sealed class CoverageRepoRow
{
    public required string Repo { get; init; }
    public required string Status { get; init; }
    public double Seconds { get; init; }
    public int TestsTotal { get; init; }
    public int TestsPassed { get; init; }
    public int TestsFailed { get; init; }
    public double? LinePercent { get; init; }
    public double? BranchPercent { get; init; }
    public string? Error { get; init; }

    public string LineDisplay => LinePercent is { } v ? $"{v:0.0}%" : "—";
    public string BranchDisplay => BranchPercent is { } v ? $"{v:0.0}%" : "—";
}
