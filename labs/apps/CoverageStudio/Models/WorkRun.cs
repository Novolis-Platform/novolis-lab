using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CoverageStudio.Models;

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
