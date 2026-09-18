using System.Numerics;
using Novolis.Rendering.PathTrace.Demos;
using Novolis.Rendering.Presentation.Abstractions;
using Novolis.Simulation.View;
using Novolis.Rendering.Runtime;

namespace MeshBench.Services;

internal sealed class PathTraceViewport : IDisposable
{
    public const int MaxAccumulatedSamples = 96;

    private readonly PathTraceDisplayBuffer _display = new();
    private readonly OrbitCameraRig _orbit = new() { Target = new Vector3(0f, 0.45f, 0f), Distance = 4.5f };
    private readonly object _resizeGate = new();

    private PathTraceSession? _session;
    private PathTraceBackgroundWorker? _worker;
    private IFramePresenter? _presenter;
    private int _width;
    private int _height;
    private int _fullWidth;
    private int _fullHeight;
    private int _queuedSamples;
    private int _lastPresentedSampleCount = -1;
    private float _renderScale = 1f;
    private int _resizeGeneration;
    private bool _paused = true;
    private string _status = "Preview mode";

    public string BackendLabel => _session?.Backend.BackendLabel ?? "path trace";

    public OrbitCameraRig Orbit => _orbit;

    public int DisplayedSamples => _display.DisplayedSampleCount;

    public bool IsReady => _width > 0 && _height > 0 && _session is not null;

    public bool LastFramePresented { get; private set; }

    public string Status => _status;

    public bool IsPaused => _paused;

    public bool IsWorkerBusy => _worker?.IsBusy ?? false;

    public void Attach(IFramePresenter presenter) => _presenter = presenter;

    public void BeginTracing()
    {
        _paused = false;
        _queuedSamples = 0;
        _lastPresentedSampleCount = -1;
        _status = $"{BackendLabel} — starting…";
    }

    public void StopTracing()
    {
        _paused = true;
        _queuedSamples = 0;
        _status = "Preview mode";
    }

    public void SetRenderScale(float scale) =>
        _renderScale = Math.Clamp(scale, 0.25f, 1f);

    public void TryResizeFromBounds(double width, double height)
    {
        var w = (int)Math.Max(0, width);
        var h = (int)Math.Max(0, height);
        if (w <= 0 || h <= 0)
        {
            _status = $"Waiting for viewport size ({w}×{h})…";
            return;
        }

        _fullWidth = Math.Min(w, 1920);
        _fullHeight = Math.Min(h, 1080);
        QueueResize();
    }

    private void QueueResize()
    {
        var w = Math.Max(64, (int)(_fullWidth * _renderScale));
        var h = Math.Max(64, (int)(_fullHeight * _renderScale));
        if (w == _width && h == _height)
            return;

        var generation = Interlocked.Increment(ref _resizeGeneration);
        _ = Task.Run(() => ApplyScaledSizeAsync(w, h, generation));
    }

    private async Task ApplyScaledSizeAsync(int w, int h, int generation)
    {
        await Task.Yield();
        if (generation != _resizeGeneration)
            return;

        var session = _session;
        var worker = _worker;
        if (session is null || worker is null)
        {
            lock (_resizeGate)
            {
                _width = w;
                _height = h;
            }

            _status = "Waiting for scene…";
            return;
        }

        await worker.WaitForIdleAsync().ConfigureAwait(false);
        if (generation != _resizeGeneration)
            return;

        session.Resize(w, h);
        if (generation != _resizeGeneration)
            return;

        lock (_resizeGate)
        {
            _width = w;
            _height = h;
            _display.Invalidate(w, h);
            _queuedSamples = 0;
            _lastPresentedSampleCount = -1;
        }

        _status = $"{BackendLabel} {_width}×{_height}";
    }

    public void SetScene(CompiledScene scene)
    {
        ReplaceSession(scene);
        if (_width > 0 && _height > 0)
            QueueResize();
        else
            _status = "Scene ready — waiting for viewport layout…";
    }

    private void ReplaceSession(CompiledScene scene)
    {
        _worker?.Dispose();
        _session?.Dispose();
        _session = new PathTraceSession(scene);
        _worker = new PathTraceBackgroundWorker(_session.Backend, _display);
        _queuedSamples = 0;
        _lastPresentedSampleCount = -1;
        if (_width > 0 && _height > 0)
        {
            _session.Resize(_width, _height);
            _display.Invalidate(_width, _height);
        }

        _status = $"{BackendLabel} scene ready";
    }

    public void ResetAccumulation()
    {
        if (_paused || _width <= 0 || _height <= 0 || _session is null || _worker is null)
            return;

        _queuedSamples = 0;
        _lastPresentedSampleCount = -1;
        var worker = _worker;
        var session = _session;
        _ = Task.Run(async () =>
        {
            await worker.WaitForIdleAsync().ConfigureAwait(false);
            session.Backend.ResetAccumulation();
            lock (_resizeGate)
            {
                _display.Invalidate(_width, _height);
            }
        });
    }

    public void Tick(int batchSize = 8)
    {
        if (_paused || _presenter is null)
        {
            LastFramePresented = false;
            return;
        }

        if (_width <= 0 || _height <= 0)
        {
            LastFramePresented = false;
            _status = "Waiting for viewport layout…";
            return;
        }

        if (_session is null || _worker is null)
        {
            LastFramePresented = false;
            _status = "Compiling scene…";
            return;
        }

        TryPresentLatest();

        if (_queuedSamples >= MaxAccumulatedSamples)
        {
            _status = $"{BackendLabel} — {DisplayedSamples} samples";
            return;
        }

        if (_worker.IsBusy)
        {
            _status = $"{BackendLabel} tracing… ({DisplayedSamples} shown)";
            return;
        }

        var camera = BuildCamera();
        var batch = Math.Min(batchSize, MaxAccumulatedSamples - _queuedSamples);
        if (batch > 0 && _worker.TryEnqueueAccumulate(camera, ref _queuedSamples, batch))
            _status = $"{BackendLabel} tracing… ({DisplayedSamples} shown)";

        TryPresentLatest();
    }

    private void TryPresentLatest()
    {
        if (_presenter is null || _worker is null || _worker.IsBusy)
            return;

        var shown = DisplayedSamples;
        if (shown <= 0 || shown == _lastPresentedSampleCount)
            return;

        if (_display.TryPresent(_presenter))
        {
            LastFramePresented = true;
            _lastPresentedSampleCount = shown;
        }
    }

    public CameraSnapshot BuildCamera()
    {
        var eye = _orbit.BuildEyePosition();
        return CameraSnapshot.LookAt(
            eye,
            _orbit.Target,
            Vector3.UnitY,
            _orbit.FieldOfViewDegrees,
            _width / (float)Math.Max(1, _height));
    }

    public void ApplyCameraState(Models.OrbitCameraState state)
    {
        _orbit.Yaw = state.Yaw;
        _orbit.Pitch = state.Pitch;
        _orbit.Distance = state.Distance;
        if (state.Target.Length >= 3)
            _orbit.Target = new Vector3(state.Target[0], state.Target[1], state.Target[2]);
    }

    public Models.OrbitCameraState CaptureCameraState() =>
        new()
        {
            Yaw = _orbit.Yaw,
            Pitch = _orbit.Pitch,
            Distance = _orbit.Distance,
            Target = [_orbit.Target.X, _orbit.Target.Y, _orbit.Target.Z],
        };

    public void FitToBounds(Vector3 center, float radius)
    {
        _orbit.Target = center;
        _orbit.Distance = MathF.Max(2f, radius * 2.8f);
        ResetAccumulation();
    }

    public void Dispose()
    {
        _worker?.Dispose();
        _session?.Dispose();
    }
}

internal static class PathTraceWorkerExtensions
{
    public static Task WaitForIdleAsync(this PathTraceBackgroundWorker worker) =>
        Task.Run(worker.WaitForIdle);
}
