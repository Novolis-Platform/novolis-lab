using Avalonia.Controls;
using MeshBench.Models;
using Novolis.Avalonia.Raylib;
using Novolis.Simulation.View;

namespace MeshBench.Services;

internal enum ViewportDisplayMode
{
    FastPreview,
    QualityRefine,
}

/// <summary>Coordinates fast Raylib preview vs path-traced quality (one GLFW host at a time).</summary>
internal sealed class ViewportModeCoordinator
{
    private readonly PathTraceViewport _pathTrace;
    private readonly RaylibHostControl _raylibHost;
    private Panel? _raylibHostParent;
    private int _raylibHostIndex = -1;
    private readonly RaylibSceneRenderer _raylibRenderer = new();
    private readonly SceneUpdateScheduler _qualityScheduler = new();
    private readonly OrbitCameraRig _orbit = new() { Target = new System.Numerics.Vector3(0f, 0.45f, 0f), Distance = 4.5f };

    private Func<MeshSceneDocument> _getScene = () => new();
    private ViewportDisplayMode _mode = ViewportDisplayMode.FastPreview;
    private bool _qualityPinned;
    private bool _interacting;

    public ViewportDisplayMode Mode => _mode;

    public bool IsInteracting => _interacting;

    public bool QualityPinned => _qualityPinned;

    public OrbitCameraRig Orbit => _orbit;

    public PathTraceViewport PathTrace => _pathTrace;

    public RaylibHostControl RaylibHost => _raylibHost;

    public event Action<ViewportDisplayMode>? ModeChanged;

    public event Action? QualityRebuildDue;

    public ViewportModeCoordinator(PathTraceViewport pathTrace, RaylibHostControl raylibHost)
    {
        _pathTrace = pathTrace;
        _raylibHost = raylibHost;
        _qualityScheduler.QualityRebuildDue += () => QualityRebuildDue?.Invoke();
        _raylibHost.FrameRendering += OnRaylibFrame;
        EnsureRaylibParentTracked();
    }

    public void BindScene(Func<MeshSceneDocument> getScene)
    {
        _getScene = getScene;
        _raylibRenderer.Bind(getScene, () => _orbit);
    }

    public void SetQualityPinned(bool pinned)
    {
        _qualityPinned = pinned;
        if (pinned)
            EnterQuality();
        else
            EnterFast();
    }

    public void NotifyInteractionStarted()
    {
        _interacting = true;
        _qualityScheduler.Cancel();
        _pathTrace.StopTracing();
        EnterFast();
    }

    public void NotifyInteractionEnded()
    {
        _interacting = false;
        if (_qualityPinned)
            EnterQuality();
        else
            _raylibHost.RequestFrame();
    }

    public void NotifySceneChanged(bool immediateQualityRebuild = false)
    {
        if (_mode == ViewportDisplayMode.FastPreview)
        {
            _raylibHost.RequestFrame();
            if (immediateQualityRebuild && _qualityPinned)
                _qualityScheduler.FlushNow();
            else if (_qualityPinned)
                _qualityScheduler.ScheduleQualityRebuild();
            return;
        }

        if (immediateQualityRebuild)
            QualityRebuildDue?.Invoke();
        else
            _qualityScheduler.ScheduleQualityRebuild();
    }

    public void ApplyCameraFromScene(OrbitCameraState state)
    {
        _orbit.Yaw = state.Yaw;
        _orbit.Pitch = state.Pitch;
        _orbit.Distance = state.Distance;
        if (state.Target.Length >= 3)
            _orbit.Target = new System.Numerics.Vector3(state.Target[0], state.Target[1], state.Target[2]);
        _pathTrace.ApplyCameraState(state);
    }

    public OrbitCameraState CaptureCameraState() =>
        new()
        {
            Yaw = _orbit.Yaw,
            Pitch = _orbit.Pitch,
            Distance = _orbit.Distance,
            Target = [_orbit.Target.X, _orbit.Target.Y, _orbit.Target.Z],
        };

    public void TickPathTrace(int batchSize = 8)
    {
        if (_mode != ViewportDisplayMode.QualityRefine)
            return;

        _pathTrace.Tick(batchSize);
    }

    public void TryResizePathTrace(double width, double height) =>
        _pathTrace.TryResizeFromBounds(width, height);

    private void EnterFast()
    {
        var modeChanged = _mode != ViewportDisplayMode.FastPreview;
        _mode = ViewportDisplayMode.FastPreview;
        _pathTrace.StopTracing();
        AttachRaylibHost();
        _raylibHost.SetHostActive(true);
        _raylibHost.EnsureHostStarted();
        _raylibHost.RequestFrame();
        if (modeChanged)
            ModeChanged?.Invoke(_mode);
    }

    private void EnterQuality()
    {
        var modeChanged = _mode != ViewportDisplayMode.QualityRefine;
        _raylibHost.SetHostActive(false);
        _mode = ViewportDisplayMode.QualityRefine;
        DetachRaylibHost();
        _pathTrace.BeginTracing();
        if (modeChanged)
            ModeChanged?.Invoke(_mode);
        QualityRebuildDue?.Invoke();
    }

    private void OnRaylibFrame(object? sender, RaylibFrameEventArgs e) =>
        _raylibRenderer.OnFrame(e.DeltaSeconds, e.ScreenWidth, e.ScreenHeight);

    public void StartInFastMode()
    {
        _qualityPinned = false;
        _pathTrace.StopTracing();
        _mode = ViewportDisplayMode.FastPreview;
        AttachRaylibHost();
        _raylibHost.SetHostActive(true);
        _raylibHost.EnsureHostStarted();
        _raylibHost.RequestFrame();
        ModeChanged?.Invoke(_mode);
    }

    private void AttachRaylibHost()
    {
        if (_raylibHost.Parent is not null)
            return;

        EnsureRaylibParentTracked();
        if (_raylibHostParent is null)
            return;

        var index = _raylibHostIndex >= 0 && _raylibHostIndex <= _raylibHostParent.Children.Count
            ? _raylibHostIndex
            : 0;
        _raylibHostParent.Children.Insert(index, _raylibHost);
    }

    private void DetachRaylibHost()
    {
        if (_raylibHost.Parent is not Panel parent)
            return;

        _raylibHostParent = parent;
        _raylibHostIndex = parent.Children.IndexOf(_raylibHost);
        parent.Children.Remove(_raylibHost);
    }

    private void EnsureRaylibParentTracked()
    {
        if (_raylibHostParent is not null)
            return;

        if (_raylibHost.Parent is Panel parent)
        {
            _raylibHostParent = parent;
            _raylibHostIndex = parent.Children.IndexOf(_raylibHost);
        }
    }
}
