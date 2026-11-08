using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CalypsoCad.Session;
using CalypsoCad.Viewport;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Rendering;
using Novolis.Math.Geometry;
using Novolis.Silk;
using Novolis.Silk.Capture;

namespace CalypsoCad.Explorer;

/// <summary>
/// Hidden Silk GLFW loop → offscreen FBO readback into <see cref="Rgba32FrameControl"/>.
/// Capture resolution is independent of the tiny GLFW context (same pattern as headless).
/// Requires Avalonia Software/Skia (not WGL) so OpenGL is free for Silk.
/// </summary>
internal sealed class CalypsoSilkViewportControl : Panel
{
    /// <summary>Small GL context; pixels come from <see cref="IGlHost.BeginCaptureTarget"/>.</summary>
    private const int ContextWindowSize = 256;

    private readonly Rgba32FrameControl _frame = new();
    private readonly TextBlock _status;
    private readonly DispatcherTimer _presentTimer;
    private Thread? _renderThread;
    private volatile bool _stop;
    private Rgba32[]? _latest;
    private int _latestW;
    private int _latestH;
    private int _frames;
    private string _lastError = "";
    private readonly object _pixelLock = new();
    private bool _orbitDragging;
    private Point _lastPointer;

    public CalypsoSilkViewportControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(24, 24, 28));
        Focusable = true;
        ClipToBounds = true;
        _frame.HorizontalAlignment = HorizontalAlignment.Stretch;
        _frame.VerticalAlignment = VerticalAlignment.Stretch;
        _frame.IsHitTestVisible = false;
        Children.Add(_frame);

        _status = new TextBlock
        {
            Margin = new Thickness(12),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
        };
        GraphicalProfileBinding.Bind(_status, TextBlock.ForegroundProperty, GraphicalProfile.MutedResourceKey);
        Children.Add(_status);

        _presentTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(33),
            DispatcherPriority.Render,
            (_, _) => Present());

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheel;
        PointerCaptureLost += (_, _) => _orbitDragging = false;
        LayoutUpdated += (_, _) => MaybeRestartForSize();
    }

    public CalypsoViewportController? Controller { get; set; }

    /// <summary>Capture width cap (panel×DPI up to this; UHD by default).</summary>
    public int FrameWidth { get; set; } = SilkCaptureSize.UhdWidth;

    /// <summary>Capture height cap (panel×DPI up to this; UHD by default).</summary>
    public int FrameHeight { get; set; } = SilkCaptureSize.UhdHeight;

    private int _runningW;
    private int _runningH;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        StartHost();
        _presentTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _presentTimer.Stop();
        StopHost();
        base.OnDetachedFromVisualTree(e);
    }

    private void MaybeRestartForSize()
    {
        if (_renderThread is not { IsAlive: true })
            return;
        ResolveCaptureSize(out var w, out var h);
        if (Math.Abs(w - _runningW) < 64 && Math.Abs(h - _runningH) < 64)
            return;
        StopHost();
        StartHost();
    }

    private void ResolveCaptureSize(out int width, out int height)
    {
        var scale = (float)(TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0);
        if (scale < 0.5f || float.IsNaN(scale))
            scale = 1f;

        var maxW = Math.Clamp(FrameWidth, 960, SilkCaptureSize.UhdWidth);
        var maxH = Math.Clamp(FrameHeight, 540, SilkCaptureSize.UhdHeight);
        var bw = Bounds.Width > 32 ? Bounds.Width : maxW;
        var bh = Bounds.Height > 32 ? Bounds.Height : maxH;
        // Panel × DPI, floored for readability, capped at configured FrameWidth/Height (4K UHD).
        width = Math.Clamp((int)Math.Ceiling(bw * scale), 960, maxW);
        height = Math.Clamp((int)Math.Ceiling(bh * scale), 540, maxH);
    }

    private void StartHost()
    {
        if (_renderThread is { IsAlive: true } || Controller is null)
            return;

        _stop = false;
        _frames = 0;
        _lastError = "";
        var controller = Controller;
        ResolveCaptureSize(out var w, out var h);
        _runningW = w;
        _runningH = h;
        _renderThread = new Thread(() => RenderLoop(controller, w, h))
        {
            IsBackground = true,
            Name = "CalypsoSilkViewport",
        };
        if (OperatingSystem.IsWindows())
            TrySetSta(_renderThread);
        _renderThread.Start();
    }

    private void StopHost()
    {
        _stop = true;
        if (_renderThread is { IsAlive: true })
            _renderThread.Join(TimeSpan.FromSeconds(3));
        _renderThread = null;
    }

    private void RenderLoop(CalypsoViewportController controller, int width, int height)
    {
        try
        {
            // Tiny GLFW context; BeginCaptureTarget owns the real pixel budget (headless pattern).
            SilkGame.Run(
                "Calypso CAD viewport",
                ContextWindowSize,
                ContextWindowSize,
                initialize: null,
                update: frame =>
                {
                    if (_stop)
                    {
                        frame.Close();
                        return;
                    }

                    if (frame.IsKeyPressed(Novolis.Silk.Key.Escape))
                        frame.Close();

                    var host = frame.Host;
                    host.BeginCaptureTarget(width, height);
                    frame.OverrideSize(width, height);
                    try
                    {
                        controller.Renderer.DrawFrame(frame, frame.DeltaSeconds);
                        if (!host.TryReadPixels(out var pixels) || pixels.Length == 0)
                            return;
                        if (pixels.Length != width * height)
                            return;

                        lock (_pixelLock)
                        {
                            if (_latest is null || _latest.Length != pixels.Length)
                                _latest = new Rgba32[pixels.Length];
                            pixels.CopyTo(_latest, 0);
                            _latestW = width;
                            _latestH = height;
                            _frames++;
                        }
                    }
                    finally
                    {
                        host.EndCaptureTarget();
                    }
                },
                renderOverlay: null,
                captureSession: null,
                visible: false);
        }
        catch (Exception ex)
        {
            _lastError = $"{ex.GetType().Name}: {ex.Message}";
            controller.Session.StatusText = $"viewport:{_lastError}";
            try
            {
                File.WriteAllText(
                    Path.Combine(Path.GetTempPath(), "calypso-silk-viewport-error.txt"),
                    ex.ToString());
            }
            catch
            {
                /* best-effort */
            }
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed && !props.IsRightButtonPressed)
            return;
        _orbitDragging = true;
        _lastPointer = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_orbitDragging || Controller is null)
            return;
        if (Controller.Session.ViewMode != CalypsoViewMode.Orbit)
            return;

        var pos = e.GetPosition(this);
        var dx = (float)(pos.X - _lastPointer.X);
        var dy = (float)(pos.Y - _lastPointer.Y);
        _lastPointer = pos;
        if (MathF.Abs(dx) < 0.01f && MathF.Abs(dy) < 0.01f)
            return;
        Controller.OrbitPointerDelta(dx, dy);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_orbitDragging)
            return;
        _orbitDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        Controller?.PointerWheel((float)e.Delta.Y);
        e.Handled = true;
    }

    private void Present()
    {
        Rgba32[]? pixels;
        int w, h, n;
        lock (_pixelLock)
        {
            n = _frames;
            if (_latest is null || _latestW <= 0 || _latestH <= 0)
            {
                _status.Text = string.IsNullOrEmpty(_lastError)
                    ? $"Silk viewport starting… frames={n}"
                    : $"Silk viewport failed: {_lastError}";
                _status.IsVisible = true;
                return;
            }

            pixels = _latest;
            w = _latestW;
            h = _latestH;
        }

        _status.IsVisible = n < 3;
        if (_status.IsVisible)
            _status.Text = $"Silk viewport live · {w}×{h} FBO · drag to orbit · wheel zoom";
        _frame.PresentCpuFrame(pixels, w, h);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void TrySetSta(Thread t)
    {
        try
        {
            t.SetApartmentState(ApartmentState.STA);
        }
        catch
        {
            /* already started / unsupported */
        }
    }
}
