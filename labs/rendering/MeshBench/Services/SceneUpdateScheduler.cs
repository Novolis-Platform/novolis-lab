using Avalonia.Threading;

namespace MeshBench.Services;

/// <summary>Debounces expensive quality-path scene rebuilds (compile + path trace upload).</summary>
internal sealed class SceneUpdateScheduler
{
    private readonly TimeSpan _delay;
    private DispatcherTimer? _timer;
    private Action? _pending;

    public SceneUpdateScheduler(TimeSpan? delay = null) =>
        _delay = delay ?? TimeSpan.FromMilliseconds(100);

    public event Action? QualityRebuildDue;

    public void ScheduleQualityRebuild()
    {
        _pending = () => QualityRebuildDue?.Invoke();
        _timer ??= new DispatcherTimer(_delay, DispatcherPriority.Background, OnTimerTick);
        _timer.Stop();
        _timer.Start();
    }

    public void FlushNow()
    {
        _timer?.Stop();
        if (_pending is not null)
        {
            _pending();
            _pending = null;
        }
    }

    public void Cancel()
    {
        _timer?.Stop();
        _pending = null;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer?.Stop();
        _pending?.Invoke();
        _pending = null;
    }
}
