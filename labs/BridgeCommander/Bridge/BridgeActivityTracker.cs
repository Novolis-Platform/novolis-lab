namespace BridgeCommander.Bridge;

/// <summary>
/// Tracks queued and in-flight commands so automation can await a quiescent bridge.
/// </summary>
public sealed class BridgeActivityTracker
{
    private int _outstanding;
    private TaskCompletionSource _idleSignal = CreateIdleSignal();

    public void OnEnqueued()
    {
        if (Interlocked.Increment(ref _outstanding) == 1)
            _idleSignal = CreateIdleSignal();
    }

    public void OnFinished()
    {
        if (Interlocked.Decrement(ref _outstanding) == 0)
            _idleSignal.TrySetResult();
    }

    public bool IsIdle => Volatile.Read(ref _outstanding) == 0;

    public async Task WaitForIdleAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (IsIdle)
            return;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (!IsIdle)
        {
            var signal = _idleSignal;
            try
            {
                await signal.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Bridge did not become idle within {timeout.TotalSeconds:0.#}s ({Volatile.Read(ref _outstanding)} command(s) still outstanding).");
            }
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _outstanding, 0);
        _idleSignal = CreateIdleSignal();
    }

    private static TaskCompletionSource CreateIdleSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
