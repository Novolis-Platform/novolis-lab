using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CalypsoCad.Host;
using CalypsoCad.Rendering;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Rendering;
using Novolis.Math.Geometry;
using Novolis.Silk;
using Novolis.Silk.Capture;

namespace CalypsoCad.Shell;

/// <summary>
/// Hidden Silk GLFW loop → RGBA into <see cref="Rgba32FrameControl"/>.
/// Requires Avalonia Software/Skia (not WGL) so OpenGL is free for Silk.
/// </summary>
internal sealed class CalypsoSilkViewportControl : Panel
{
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
    }

    public CalypsoViewportController? Controller { get; set; }
    public int FrameWidth { get; set; } = 960;
    public int FrameHeight { get; set; } = 640;

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

    private void StartHost()
    {
        if (_renderThread is { IsAlive: true } || Controller is null)
            return;

        _stop = false;
        _frames = 0;
        _lastError = "";
        var controller = Controller;
        var w = Math.Max(320, FrameWidth);
        var h = Math.Max(240, FrameHeight);
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
        using var capture = new FrameCaptureSession(new CaptureStreamOptions
        {
            CaptureEveryNFrames = 1,
            MaxBufferedFrames = 2,
        });

        try
        {
            SilkGame.Run(
                "Calypso CAD viewport",
                width,
                height,
                initialize: null,
                update: frame =>
                {
                    if (_stop)
                    {
                        frame.Close();
                        return;
                    }

                    // Escape still closes the hidden GLFW loop; mouse comes from Avalonia.
                    if (frame.IsKeyPressed(Novolis.Silk.Key.Escape))
                        frame.Close();
                    controller.Renderer.DrawFrame(frame, frame.DeltaSeconds);
                    capture.CaptureAfterDraw(frame.Host);
                    if (!capture.TryRead(out var shot) || shot.Pixels is not { Length: > 0 })
                        return;

                    lock (_pixelLock)
                    {
                        if (_latest is null || _latest.Length != shot.Pixels.Length)
                            _latest = new Rgba32[shot.Pixels.Length];
                        shot.Pixels.CopyTo(_latest, 0);
                        _latestW = shot.Width;
                        _latestH = shot.Height;
                        _frames++;
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
            _status.Text = $"Silk viewport live · {w}×{h} · drag to orbit · wheel zoom";
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
