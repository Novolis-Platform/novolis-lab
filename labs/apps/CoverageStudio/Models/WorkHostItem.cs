using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CoverageStudio.Models;

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
