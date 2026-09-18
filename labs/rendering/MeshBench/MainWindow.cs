using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MeshBench.Models;
using MeshBench.Services;
using MeshBench.Ui;
using Novolis.Avalonia.Raylib;
using Novolis.Avalonia.Rendering;
using Novolis.Avalonia.Studio;
using Novolis.Timeline.Presentation.GitGraph;
namespace MeshBench;

internal sealed class MainWindow : Window
{
    private const int QualitySampleTarget = 192;
    private const int QualitySampleThrottle = 64;

    private readonly MeshBenchSession _session;
    private readonly MeshSceneStore _scenes;
    private readonly PathTraceViewport _viewport;
    private readonly SavePointSound _sound;
    private readonly ViewportModeCoordinator _coordinator;

    private readonly RaylibHostControl _raylibHost = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };
    private readonly Rgba32FrameControl _frame = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };
    private Grid _viewportHost = null!;
    private readonly TextBlock _viewportHint = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        FontSize = 14,
        Opacity = 0.85,
        IsHitTestVisible = false,
    };
    private readonly GitHistoryPanel _gitHistory = new();
    private readonly ListBox _partsList = new();
    private readonly PartInspectorPanel _inspector = new();
    private readonly TextBlock _status = new() { Margin = new Thickness(8, 4) };
    private readonly TextBlock _flash = new()
    {
        Margin = new Thickness(8, 0, 8, 4),
        FontWeight = FontWeight.SemiBold,
        Foreground = Brushes.LightGreen,
    };
    private readonly TextBlock _workspacePath = new() { Opacity = 0.7, FontSize = 11 };
    private readonly Border _busyOverlay = new()
    {
        Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
        IsVisible = false,
        IsHitTestVisible = true,
    };
    private readonly TextBlock _busyText = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        FontSize = 16,
        FontWeight = FontWeight.Bold,
    };

    private readonly Button _previewModeButton;
    private readonly Button _qualityModeButton;

    private StudioFeedback _feedback = null!;
    private MeshPartRecord? _selectedPart;
    private GitGraphTimelineRow? _selectedGitRow;
    private bool _orbiting;
    private bool _movingPart;
    private int _saveInFlight;
    private int _qualityRebuildInFlight;
    private int _boxCounter;
    private int _sphereCounter;
    private Point _lastPointer;
    private DispatcherTimer? _renderTimer;
    private DispatcherTimer? _editIdleTimer;
    private bool _inspectorEditActive;
    private CancellationTokenSource? _rebuildCts;
    private Size _lastViewportSize;
    private int _qualityTick;
    private int _uiTick;
    private string _lastStatusText = string.Empty;
    public MainWindow(MeshBenchSession session, MeshSceneStore scenes, PathTraceViewport viewport, SavePointSound sound)
    {
        _session = session;
        _scenes = scenes;
        _viewport = viewport;
        _sound = sound;
        _coordinator = new ViewportModeCoordinator(viewport, _raylibHost);

        Title = "Mesh Studio";
        Width = 1480;
        Height = 920;

        _previewModeButton = Button("Preview", OnPreviewMode);
        _qualityModeButton = Button("Quality", OnQualityMode);

        Content = BuildLayout();
        _feedback = new StudioFeedback(_status, _flash, _busyOverlay, _busyText);

        _coordinator.BindScene(() => _session.Scene);
        _coordinator.ModeChanged += OnViewportModeChanged;
        _coordinator.QualityRebuildDue += OnQualityRebuildDue;

        Opened += OnOpened;
        KeyDown += OnKeyDown;
    }

    private Control BuildLayout()
    {
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8),
        };

        toolbar.Children.Add(Button("Save", OnSavePoint, "Ctrl+S"));
        toolbar.Children.Add(Button("Restore", OnRestore));
        toolbar.Children.Add(Button("Branch", OnBranch));
        toolbar.Children.Add(Separator());
        toolbar.Children.Add(Button("Box", OnAddBox, "B"));
        toolbar.Children.Add(Button("Sphere", OnAddSphere, "S"));
        toolbar.Children.Add(Button("Duplicate", OnDuplicate, "Ctrl+D"));
        toolbar.Children.Add(Button("Delete", OnDeletePart, "Del"));
        toolbar.Children.Add(Separator());
        toolbar.Children.Add(Button("Fit view", OnFitView, "F"));
        toolbar.Children.Add(Separator());
        toolbar.Children.Add(_previewModeButton);
        toolbar.Children.Add(_qualityModeButton);

        _viewportHost = new Grid();
        _viewportHost.Children.Add(_raylibHost);
        _viewportHost.Children.Add(_frame);
        _viewportHost.Children.Add(_viewportHint);
        _viewportHost.Children.Add(_busyOverlay);

        _viewportHost.PointerPressed += OnViewportPointerPressed;
        _viewportHost.PointerReleased += OnViewportPointerReleased;
        _viewportHost.PointerMoved += OnViewportPointerMoved;
        _viewportHost.PointerWheelChanged += OnViewportWheel;
        _viewportHost.LayoutUpdated += (_, _) => EnsureViewportSized();

        _frame.IsVisible = false;

        _gitHistory.SelectionChanged += (_, _) =>
        {
            _selectedGitRow = _gitHistory.SelectedRow;
            if (_selectedGitRow is not null)
                _feedback.Flash($"Snapshot: {_selectedGitRow.Subject}");
        };
        _gitHistory.RestoreRequested += (_, row) =>
        {
            _selectedGitRow = row;
            OnRestore(this, new RoutedEventArgs());
        };

        _partsList.ItemTemplate = new FuncDataTemplate<MeshPartRecord>((part, _) =>
            new TextBlock { Text = part?.Summary ?? string.Empty },
            supportsRecycling: true);

        _partsList.SelectionChanged += (_, _) =>
        {
            _selectedPart = _partsList.SelectedItem as MeshPartRecord;
            _inspector.Bind(_selectedPart);
            _feedback.Flash(_selectedPart is null ? "Selection cleared" : $"Selected {_selectedPart.Name}");
        };

        _inspector.PartChanged += (_, _) => OnInspectorEdited();

        _busyOverlay.Child = _busyText;

        var viewportPanel = new DockPanel();
        DockPanel.SetDock(_flash, Dock.Bottom);
        DockPanel.SetDock(_status, Dock.Bottom);
        viewportPanel.Children.Add(_flash);
        viewportPanel.Children.Add(_status);
        viewportPanel.Children.Add(_viewportHost);

        var center = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        center.Children.Add(toolbar);
        Grid.SetRow(viewportPanel, 1);
        center.Children.Add(viewportPanel);

        var right = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        var meshesPanel = new DockPanel();
        var meshesHeader = new TextBlock
        {
            Text = "Meshes",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(8, 8, 8, 4),
        };
        DockPanel.SetDock(meshesHeader, Dock.Top);
        meshesPanel.Children.Add(meshesHeader);
        meshesPanel.Children.Add(_partsList);
        Grid.SetRow(meshesPanel, 0);
        right.Children.Add(meshesPanel);

        var inspectorHost = new DockPanel();
        var inspectorHeader = new TextBlock
        {
            Text = "Inspector",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(8, 8, 8, 4),
        };
        DockPanel.SetDock(inspectorHeader, Dock.Top);
        inspectorHost.Children.Add(inspectorHeader);
        inspectorHost.Children.Add(_inspector);
        Grid.SetRow(inspectorHost, 1);
        right.Children.Add(inspectorHost);

        var timelinePanel = new DockPanel();
        var timelineHeader = new TextBlock
        {
            Text = "History (git graph)",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(8, 8, 8, 4),
        };
        DockPanel.SetDock(timelineHeader, Dock.Top);
        DockPanel.SetDock(_workspacePath, Dock.Bottom);
        _workspacePath.Margin = new Thickness(8);
        timelinePanel.Children.Add(timelineHeader);
        timelinePanel.Children.Add(_workspacePath);
        _gitHistory.VerticalAlignment = VerticalAlignment.Stretch;
        timelinePanel.Children.Add(_gitHistory);

        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*,320") };
        Grid.SetColumn(timelinePanel, 0);
        root.Children.Add(timelinePanel);
        Grid.SetColumn(center, 1);
        root.Children.Add(center);
        Grid.SetColumn(right, 2);
        root.Children.Add(right);

        return root;
    }

    private static Control Separator() => new Border { Width = 8 };

    private static Button Button(string text, EventHandler<RoutedEventArgs> click, string? tooltip = null)
    {
        var button = new Button { Content = text };
        if (tooltip is not null)
            ToolTip.SetTip(button, tooltip);
        button.Click += click;
        return button;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        _feedback.Flash("Opening workspace…");
        try
        {
            _viewport.Attach(_frame);
            await _session.OpenOrCreateDefaultAsync();
            _workspacePath.Text = _session.Workspace?.Root.FullName ?? string.Empty;
            _boxCounter = _session.Scene.Parts.Count(p => p.Kind.Equals("box", StringComparison.OrdinalIgnoreCase));
            _sphereCounter = _session.Scene.Parts.Count(p => p.Kind.Equals("sphere", StringComparison.OrdinalIgnoreCase));
            SyncCameraFromScene();
            _coordinator.StartInFastMode();
            OnViewportModeChanged(_coordinator.Mode);
            StartRenderLoop();
            Dispatcher.UIThread.Post(EnsureViewportSized, DispatcherPriority.Loaded);
            _feedback.Flash($"Workspace ready — {_session.Scene.Parts.Count} meshes");
        }
        catch (Exception ex)
        {
            _feedback.FlashError($"Open failed: {ex.Message}");
        }
        finally
        {
            RefreshUi();
        }
    }

    private void StartRenderLoop()
    {
        _renderTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Background, (_, _) =>
        {
            EnsureViewportSized();

            if (ShouldRunQualityPass())
            {
                _qualityTick++;
                if ((_qualityTick & 1) == 0)
                {
                    var samples = _viewport.DisplayedSamples;
                    var batch = samples >= QualitySampleTarget ? 2 : samples >= QualitySampleThrottle ? 4 : 8;
                    _coordinator.TickPathTrace(batch);
                }
            }

            if ((_uiTick++ & 7) == 0)
            {
                UpdateViewportHint();
                UpdateStatus();
            }
        });
        _renderTimer.Start();
    }

    private bool ShouldRunQualityPass() =>
        _coordinator.QualityPinned
        && _coordinator.Mode == ViewportDisplayMode.QualityRefine
        && !_coordinator.IsInteracting
        && !_movingPart
        && !_orbiting
        && !_inspectorEditActive;

    private void EnsureViewportSized()
    {
        var size = _viewportHost.Bounds.Size;
        if (size.Width < 1 || size.Height < 1)
            return;

        if (Math.Abs(size.Width - _lastViewportSize.Width) < 1
            && Math.Abs(size.Height - _lastViewportSize.Height) < 1)
            return;

        _lastViewportSize = size;
        _raylibHost.FrameWidth = (int)size.Width;
        _raylibHost.FrameHeight = (int)size.Height;
        if (_coordinator.Mode == ViewportDisplayMode.FastPreview)
        {
            _raylibHost.EnsureHostStarted();
            _raylibHost.RequestFrame();
        }

        if (_coordinator.Mode == ViewportDisplayMode.QualityRefine)
            _coordinator.TryResizePathTrace(size.Width, size.Height);
    }

    private void OnViewportModeChanged(ViewportDisplayMode mode)
    {
        var fast = mode == ViewportDisplayMode.FastPreview;
        _raylibHost.IsVisible = fast;
        _frame.IsVisible = !fast;
        _viewport.SetRenderScale(fast ? 1f : _orbiting || _movingPart ? 0.5f : 1f);
        _lastViewportSize = default;
        UpdateModeButtons();
        Dispatcher.UIThread.Post(() =>
        {
            EnsureViewportSized();
            if (!fast)
                _ = RebuildQualityViewportAsync();
        }, DispatcherPriority.Loaded);
    }

    private void UpdateModeButtons()
    {
        var preview = _coordinator.Mode == ViewportDisplayMode.FastPreview;
        _previewModeButton.FontWeight = preview ? FontWeight.Bold : FontWeight.Normal;
        _qualityModeButton.FontWeight = preview ? FontWeight.Normal : FontWeight.Bold;
        _previewModeButton.Content = preview ? "● Preview" : "Preview";
        _qualityModeButton.Content = preview ? "Quality" : "● Quality";
    }

    private void OnPreviewMode(object? sender, RoutedEventArgs e)
    {
        _coordinator.SetQualityPinned(false);
        _feedback.Flash("Preview — instant editing");
        UpdateModeButtons();
    }

    private void OnQualityMode(object? sender, RoutedEventArgs e)
    {
        _coordinator.SetQualityPinned(true);
        _feedback.Flash("Quality — path tracing (edits switch back to Preview)");
        UpdateModeButtons();
    }

    private void UpdateViewportHint()
    {
        if (_coordinator.Mode == ViewportDisplayMode.FastPreview)
        {
            _viewportHint.IsVisible = false;
            return;
        }

        if (_viewport.IsReady && _viewport.LastFramePresented)
        {
            _viewportHint.IsVisible = false;
            return;
        }

        _viewportHint.IsVisible = true;
        _viewportHint.Text = _viewport.Status;
    }

    private void UpdateStatus()
    {
        var dirty = _session.IsDirty ? "  ● unsaved" : string.Empty;
        var partCount = _session.Scene.Parts.Count;
        var mode = _coordinator.Mode == ViewportDisplayMode.FastPreview ? "Preview" : "Quality";
        string present;
        if (_coordinator.Mode == ViewportDisplayMode.FastPreview)
        {
            var host = _coordinator.RaylibHost.IsHostRunning ? "host on" : "host off";
            var age = _coordinator.RaylibHost.LastFrameAgeMs;
            var frame = age >= 0 ? $"frame {age:F0}ms ago" : "no frame yet";
            present = $"Raylib {host} | {frame}";
        }
        else
        {
            present = _coordinator.IsInteracting ? "paused" : _viewport.LastFramePresented ? "shown" : "tracing";
        }
        var hint = _movingPart
            ? "Shift+drag: move  |  drag: orbit  |  wheel: zoom"
            : "Shift+drag moves selection  |  Ctrl+S save";
        var text =
            $"{mode}  |  {partCount} mesh{(partCount == 1 ? string.Empty : "es")}{dirty}  |  {present}  |  {_viewport.Status}  |  {hint}";
        if (text == _lastStatusText)
            return;

        _lastStatusText = text;
        _feedback.SetStatus(text);
    }

    private void RefreshUi()
    {
        if (_partsList.ItemsSource is not IList<MeshPartRecord> list
            || list.Count != _session.Scene.Parts.Count)
        {
            _partsList.ItemsSource = _session.Scene.Parts.ToList();
        }
        else
        {
            for (var i = 0; i < _session.Scene.Parts.Count; i++)
                list[i] = _session.Scene.Parts[i];
        }

        _gitHistory.SetRows(_session.GitGraphRows);

        if (_selectedPart is not null && !_session.Scene.Parts.Contains(_selectedPart))
            _selectedPart = null;
        _inspector.Bind(_selectedPart);
        UpdateStatus();
    }

    private void OnInspectorEdited()
    {
        _inspectorEditActive = true;
        _coordinator.NotifyInteractionStarted();
        OnSceneEdited(bumpGeometry: true);
        _editIdleTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) =>
        {
            _editIdleTimer?.Stop();
            _inspectorEditActive = false;
            _coordinator.NotifyInteractionEnded();
        });
        _editIdleTimer.Stop();
        _editIdleTimer.Start();
    }

    private void OnSceneEdited(bool bumpGeometry = true)
    {
        _session.MarkDirty();
        if (bumpGeometry)
            _session.BumpSceneGeometry();
        _coordinator.NotifySceneChanged(immediateQualityRebuild: false);
        if (_selectedPart is not null)
            RefreshUi();
        else
            UpdateStatus();
    }

    private void OnQualityRebuildDue() => _ = RebuildQualityViewportAsync();

    private async Task RebuildQualityViewportAsync()
    {
        if (!_coordinator.QualityPinned || _coordinator.Mode != ViewportDisplayMode.QualityRefine || _coordinator.IsInteracting)
            return;

        _rebuildCts?.Cancel();
        _rebuildCts = new CancellationTokenSource();
        var ct = _rebuildCts.Token;

        if (Interlocked.CompareExchange(ref _qualityRebuildInFlight, 1, 0) != 0)
            return;

        try
        {
            _session.Scene.Camera = _coordinator.CaptureCameraState();
            SyncPathTraceCamera();

            var revision = _session.SceneRevision;
            var document = _session.Scene;
            var compiled = await Task.Run(() => _scenes.Compile(document, revision), ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested || revision != _session.SceneRevision)
                return;

            _viewport.SetScene(compiled);
            _viewport.BeginTracing();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer edit.
        }
        finally
        {
            Interlocked.Exchange(ref _qualityRebuildInFlight, 0);
        }
    }

    private void SyncCameraFromScene()
    {
        _coordinator.ApplyCameraFromScene(_session.Scene.Camera);
        SyncPathTraceCamera();
        EnsureViewportSized();
    }

    private void SyncPathTraceCamera() =>
        _viewport.ApplyCameraState(_coordinator.CaptureCameraState());

    private void OnFitView(object? sender, RoutedEventArgs e)
    {
        var (center, radius) = SceneBounds.Compute(_session.Scene);
        _coordinator.Orbit.Target = center;
        _coordinator.Orbit.Distance = MathF.Max(2f, radius * 2.8f);
        _session.Scene.Camera = _coordinator.CaptureCameraState();
        SyncPathTraceCamera();
        if (_coordinator.Mode == ViewportDisplayMode.QualityRefine)
            _viewport.ResetAccumulation();
        OnSceneEdited(bumpGeometry: false);
        _feedback.Flash("Fit view");
    }

    private async void OnSavePoint(object? sender, RoutedEventArgs e)
    {
        if (Interlocked.CompareExchange(ref _saveInFlight, 1, 0) != 0)
        {
            _feedback.FlashWarning("Save already in progress…");
            return;
        }

        try
        {
            await _feedback.RunAsync(
                "Saving scene…",
                "Save point created",
                async () =>
                {
                    _session.Scene.Camera = _coordinator.CaptureCameraState();
                    await _session.SavePointAsync("Manual save");
                    _sound.Play();
                    RefreshUi();
                });
        }
        catch
        {
            // RunAsync already flashed error.
        }
        finally
        {
            Interlocked.Exchange(ref _saveInFlight, 0);
        }
    }

    private async void OnRestore(object? sender, RoutedEventArgs e)
    {
        if (_selectedGitRow is null)
        {
            _feedback.FlashWarning("Select a snapshot in the history graph");
            return;
        }

        try
        {
            await _feedback.RunAsync(
                $"Restoring {_selectedGitRow.Subject}…",
                "Restore complete",
                async () =>
                {
                    await _session.RestoreAsync(_selectedGitRow.Id);
                    SyncCameraFromScene();
                    _coordinator.StartInFastMode();
                    OnViewportModeChanged(_coordinator.Mode);
                    _coordinator.NotifySceneChanged(immediateQualityRebuild: true);
                    RefreshUi();
                });
        }
        catch
        {
            // Flashed in RunAsync.
        }
    }

    private async void OnBranch(object? sender, RoutedEventArgs e)
    {
        if (_selectedGitRow is null)
        {
            _feedback.FlashWarning("Select a snapshot to branch from");
            return;
        }

        try
        {
            var name = $"branch-{_session.GitGraphRows.Count}";
            await _feedback.RunAsync(
                $"Branching from {_selectedGitRow.Subject}…",
                $"Branch '{name}' created",
                async () =>
                {
                    await _session.BranchAsync(_selectedGitRow.Id, name);
                    RefreshUi();
                });
        }
        catch
        {
            // Flashed in RunAsync.
        }
    }

    private void OnAddBox(object? sender, RoutedEventArgs e)
    {
        _boxCounter++;
        var part = new MeshPartRecord
        {
            Name = $"Box {_boxCounter}",
            Kind = "box",
            Center = [Random.Shared.NextSingle() * 0.8f - 0.4f, 0.5f, Random.Shared.NextSingle() * 0.8f - 0.4f],
            HalfExtents = [0.35f, 0.35f, 0.35f],
            Color = [Random.Shared.NextSingle() * 0.4f + 0.45f, Random.Shared.NextSingle() * 0.35f + 0.35f, 0.32f],
        };
        _session.Scene.Parts.Add(part);
        _selectedPart = part;
        _coordinator.NotifyInteractionStarted();
        OnSceneEdited();
        RefreshUi();
        _partsList.SelectedItem = part;
        _inspector.Bind(part);
        _coordinator.NotifyInteractionEnded();
        _feedback.Flash($"Added {part.Name}");
    }

    private void OnAddSphere(object? sender, RoutedEventArgs e)
    {
        _sphereCounter++;
        var part = new MeshPartRecord
        {
            Name = $"Sphere {_sphereCounter}",
            Kind = "sphere",
            Center = [Random.Shared.NextSingle() * 1.2f - 0.2f, 0.55f, Random.Shared.NextSingle() * 1.2f - 0.2f],
            Radius = 0.3f + Random.Shared.NextSingle() * 0.25f,
            Color = [0.3f, Random.Shared.NextSingle() * 0.45f + 0.4f, 0.75f],
        };
        _session.Scene.Parts.Add(part);
        _selectedPart = part;
        _coordinator.NotifyInteractionStarted();
        OnSceneEdited();
        RefreshUi();
        _partsList.SelectedItem = part;
        _inspector.Bind(part);
        _coordinator.NotifyInteractionEnded();
        _feedback.Flash($"Added {part.Name}");
    }

    private void OnDuplicate(object? sender, RoutedEventArgs e)
    {
        if (_selectedPart is null)
        {
            _feedback.FlashWarning("Select a mesh to duplicate");
            return;
        }

        var copy = _selectedPart.Clone();
        copy.Center[0] += 0.35f;
        _session.Scene.Parts.Add(copy);
        _selectedPart = copy;
        OnSceneEdited();
        RefreshUi();
        _partsList.SelectedItem = copy;
        _inspector.Bind(copy);
        _feedback.Flash($"Duplicated as {copy.Name}");
    }

    private void OnDeletePart(object? sender, RoutedEventArgs e)
    {
        if (_selectedPart is null)
        {
            _feedback.FlashWarning("Select a mesh to delete");
            return;
        }

        var name = _selectedPart.Name;
        _session.Scene.Parts.Remove(_selectedPart);
        _selectedPart = null;
        _inspector.Bind(null);
        OnSceneEdited();
        RefreshUi();
        _feedback.Flash($"Deleted {name}");
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.S)
        {
            OnSavePoint(sender, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.D)
        {
            OnDuplicate(sender, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Delete:
                OnDeletePart(sender, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F:
                OnFitView(sender, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.B:
                OnAddBox(sender, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.S when !e.KeyModifiers.HasFlag(KeyModifiers.Control):
                OnAddSphere(sender, new RoutedEventArgs());
                e.Handled = true;
                break;
        }
    }

    private void OnViewportPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _lastPointer = e.GetPosition((Control)sender!);
        _movingPart = e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _selectedPart is not null;
        _orbiting = !_movingPart;
        _coordinator.NotifyInteractionStarted();
        if (_movingPart)
            _feedback.Flash($"Moving {_selectedPart!.Name}");
    }

    private void OnViewportPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_movingPart)
        {
            _session.BumpSceneGeometry();
            _coordinator.NotifySceneChanged(immediateQualityRebuild: true);
        }

        _orbiting = false;
        _movingPart = false;
        _coordinator.NotifyInteractionEnded();
    }

    private void OnViewportPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition((Control)sender!);
        var delta = pos - _lastPointer;
        _lastPointer = pos;

        if (_movingPart && _selectedPart is not null)
        {
            var scale = _coordinator.Orbit.Distance * 0.0025f;
            _selectedPart.Center[0] += (float)delta.X * scale;
            _selectedPart.Center[2] -= (float)delta.Y * scale;
            _inspector.Bind(_selectedPart);
            _session.MarkDirty();
            _coordinator.NotifySceneChanged(immediateQualityRebuild: false);
            RefreshUi();
            return;
        }

        if (!_orbiting)
            return;

        _coordinator.Orbit.AddLookDelta((float)delta.X * 0.008f, (float)delta.Y * -0.008f);
        SyncPathTraceCamera();
        if (_coordinator.Mode == ViewportDisplayMode.FastPreview)
            _coordinator.RaylibHost.RequestFrame();
        else if (_coordinator.QualityPinned)
            _viewport.ResetAccumulation();
    }

    private void OnViewportWheel(object? sender, PointerWheelEventArgs e)
    {
        _coordinator.Orbit.AdjustDistance((float)-e.Delta.Y * 0.15f);
        SyncPathTraceCamera();
        if (_coordinator.Mode == ViewportDisplayMode.FastPreview)
            _coordinator.RaylibHost.RequestFrame();
        else if (_coordinator.QualityPinned)
            _viewport.ResetAccumulation();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        EnsureViewportSized();
    }
}
