using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CoverageStudio.Models;

internal sealed class RepoListItem
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public int HostCount { get; init; }
    public bool IsSelected { get; set; } = true;
    public string? Solution { get; init; }
}
