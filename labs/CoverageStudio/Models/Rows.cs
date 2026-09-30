using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CoverageStudio.Models;

internal enum WorkKind
{
    Tests,
    Coverage,
}

internal enum WorkPhase
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
}

internal enum HostPhase
{
    Queued,
    Building,
    Testing,
    Parsing,
    Succeeded,
    Failed,
    Cancelled,
}

internal sealed class WorkHostItem : INotifyPropertyChanged
{
    private HostPhase _phase = HostPhase.Queued;
    private double _progress;
    private int _testsTotal;
    private int _testsPassed;
    private int _testsFailed;
    private double? _linePercent;
    private double? _branchPercent;
    private string? _error;
    private double _seconds;
    private int? _exitCode;
    private double _liveElapsedSeconds;
    private double? _liveTimeoutSeconds;

    public required string Id { get; init; }
    public required string Repo { get; init; }
    public required string HostName { get; init; }
    public required string ProjectPath { get; init; }
    public required string WorkingDirectory { get; init; }

    public HostPhase Phase
    {
        get => _phase;
        set
        {
            if (Set(ref _phase, value))
            {
                OnPropertyChanged(nameof(StatusLabel));
                OnPropertyChanged(nameof(ResultLabel));
            }
        }
    }

    public double Progress
    {
        get => _progress;
        set => Set(ref _progress, Math.Clamp(value, 0, 1));
    }

    public double LiveElapsedSeconds
    {
        get => _liveElapsedSeconds;
        set
        {
            if (Set(ref _liveElapsedSeconds, value))
                OnPropertyChanged(nameof(ResultLabel));
        }
    }

    public double? LiveTimeoutSeconds
    {
        get => _liveTimeoutSeconds;
        set
        {
            if (Set(ref _liveTimeoutSeconds, value))
                OnPropertyChanged(nameof(ResultLabel));
        }
    }

    public int TestsTotal
    {
        get => _testsTotal;
        set { if (Set(ref _testsTotal, value)) OnPropertyChanged(nameof(ResultLabel)); }
    }

    public int TestsPassed
    {
        get => _testsPassed;
        set { if (Set(ref _testsPassed, value)) OnPropertyChanged(nameof(ResultLabel)); }
    }

    public int TestsFailed
    {
        get => _testsFailed;
        set { if (Set(ref _testsFailed, value)) OnPropertyChanged(nameof(ResultLabel)); }
    }

    public double? LinePercent
    {
        get => _linePercent;
        set { if (Set(ref _linePercent, value)) OnPropertyChanged(nameof(ResultLabel)); }
    }

    public double? BranchPercent
    {
        get => _branchPercent;
        set { if (Set(ref _branchPercent, value)) OnPropertyChanged(nameof(ResultLabel)); }
    }

    public string? Error
    {
        get => _error;
        set { if (Set(ref _error, value)) OnPropertyChanged(nameof(ResultLabel)); }
    }

    public double Seconds
    {
        get => _seconds;
        set { if (Set(ref _seconds, value)) OnPropertyChanged(nameof(ResultLabel)); }
    }

    public int? ExitCode
    {
        get => _exitCode;
        set => Set(ref _exitCode, value);
    }

    public string? CoberturaPath { get; set; }

    public string DisplayName => $"{Repo}/{HostName}";

    public string StatusLabel => Phase switch
    {
        HostPhase.Queued => "Queued",
        HostPhase.Building => "Build",
        HostPhase.Testing => "Test",
        HostPhase.Parsing => "Parse",
        HostPhase.Succeeded => "OK",
        HostPhase.Failed => "Fail",
        HostPhase.Cancelled => "Cancel",
        _ => Phase.ToString(),
    };

    public string ResultLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Error))
                return Error!;
            if (Phase is HostPhase.Building or HostPhase.Testing)
            {
                if (LiveTimeoutSeconds is { } limit && limit > 0)
                    return $"{LiveElapsedSeconds:0}s / {limit:0}s";
                if (LiveElapsedSeconds > 0)
                    return $"{LiveElapsedSeconds:0}s";
            }

            if (LinePercent is { } line)
                return $"{line:0.0}% L · {BranchPercent:0.0}% B · {TestsPassed}/{TestsTotal} · {Seconds:0.0}s";
            if (TestsTotal > 0)
                return $"{TestsPassed}/{TestsTotal} passed · {Seconds:0.0}s";
            if (Phase is HostPhase.Succeeded)
                return $"{Seconds:0.0}s";
            return "";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class WorkRun : INotifyPropertyChanged
{
    private WorkPhase _phase = WorkPhase.Queued;
    private double _progress;
    private string _title = "";
    private string _detail = "";
    private int _completed;
    private int _failed;
    private int _running;
    private double _elapsedSeconds;
    private string? _htmlIndexPath;

    public required WorkKind Kind { get; init; }
    public ObservableCollection<WorkHostItem> Hosts { get; } = [];

    public WorkPhase Phase
    {
        get => _phase;
        set { if (Set(ref _phase, value)) OnPropertyChanged(nameof(StatusLabel)); }
    }

    public double Progress
    {
        get => _progress;
        set => Set(ref _progress, Math.Clamp(value, 0, 1));
    }

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public string Detail
    {
        get => _detail;
        set => Set(ref _detail, value);
    }

    public int Completed
    {
        get => _completed;
        set { if (Set(ref _completed, value)) OnPropertyChanged(nameof(CountsLabel)); }
    }

    public int Failed
    {
        get => _failed;
        set { if (Set(ref _failed, value)) OnPropertyChanged(nameof(CountsLabel)); }
    }

    public int Running
    {
        get => _running;
        set { if (Set(ref _running, value)) OnPropertyChanged(nameof(CountsLabel)); }
    }

    public double ElapsedSeconds
    {
        get => _elapsedSeconds;
        set { if (Set(ref _elapsedSeconds, value)) OnPropertyChanged(nameof(CountsLabel)); }
    }

    public string? HtmlIndexPath
    {
        get => _htmlIndexPath;
        set => Set(ref _htmlIndexPath, value);
    }

    public string StatusLabel => Phase switch
    {
        WorkPhase.Queued => "Queued",
        WorkPhase.Running => "Running",
        WorkPhase.Succeeded => Failed > 0 ? "Completed with failures" : "Succeeded",
        WorkPhase.Failed => "Failed",
        WorkPhase.Cancelled => "Cancelled",
        _ => Phase.ToString(),
    };

    public string CountsLabel =>
        $"{Completed}/{Hosts.Count} done · {Running} active · {Failed} failed · {ElapsedSeconds:0.0}s";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Recalculate()
    {
        var total = Math.Max(1, Hosts.Count);
        Completed = Hosts.Count(h => h.Phase is HostPhase.Succeeded or HostPhase.Failed or HostPhase.Cancelled);
        Failed = Hosts.Count(h => h.Phase is HostPhase.Failed);
        Running = Hosts.Count(h => h.Phase is HostPhase.Building or HostPhase.Testing or HostPhase.Parsing);
        Progress = Hosts.Count == 0
            ? 0
            : Hosts.Average(h => h.Phase is HostPhase.Succeeded or HostPhase.Failed or HostPhase.Cancelled
                ? 1.0
                : h.Progress) ;
        Detail = CountsLabel;
        _ = total;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class RepoListItem
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public int HostCount { get; init; }
    public bool IsSelected { get; set; } = true;
    public string? Solution { get; init; }
}

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

internal sealed class PackageGapRow
{
    public required string Package { get; init; }
    public double LinePercent { get; init; }
    public double BranchPercent { get; init; }
    public int LineGap { get; init; }
    public int BranchGap { get; init; }
}

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
