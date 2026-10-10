using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CoverageStudio.Models;

internal sealed class ComplexityRow
{
    public double Crap { get; init; }
    public int Complexity { get; init; }
    public double LinePercent { get; init; }
    public double BranchPercent { get; init; }
    public required string Package { get; init; }
    public required string Method { get; init; }
    public string? File { get; init; }
    public bool Flagged { get; init; }
}
