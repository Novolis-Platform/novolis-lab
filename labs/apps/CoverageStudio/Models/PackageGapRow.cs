using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CoverageStudio.Models;

internal sealed class PackageGapRow
{
    public required string Package { get; init; }
    public double LinePercent { get; init; }
    public double BranchPercent { get; init; }
    public int LineGap { get; init; }
    public int BranchGap { get; init; }
}
