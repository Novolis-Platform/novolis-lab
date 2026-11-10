using System.Numerics;
using Microsoft.Extensions.Logging;
using Novolis.Rendering.Backends.Igpu;
using Novolis.Rendering.PathTrace.Demos;
using Novolis.Rendering.Runtime;
using Novolis.Silk;
using Novolis.Silk.Capture;
using Novolis.Simulation.View;

namespace SilkTraceStudio;

internal static class Program
{
    // Motion: multi-spp GPU batch per pose, one DMA readback (do not cancel mid-batch).
    private const int MovingPreviewSamplesGpu = 8;
    private const int MovingPreviewSamplesCpu = 4;
    // Settled 4K batches stay short so the first clean blit arrives before a multi-second stall.
    private const int AccumulateSamplesPerBatchGpu = 8;
    private const int AccumulateSamplesPerBatchCpu = 2;
    // Preview while moving; one intentional wipe to 4K after settle (not a dither between close sizes).
    private const int PreviewTraceWidth = 640;
    private const int PreviewTraceHeight = 360;
    private const int SettleTraceWidth = 3840;
    private const int SettleTraceHeight = 2160;
    private const float MovingSettleSeconds = 0.2f;
    private const float AutoOrbitRadiansPerSecond = 0.35f;
    private const int MaxProgressiveSamples = 8192;
    private const float MouseLookSensitivity = 0.004f;
    private const float ScrollZoomFactor = 0.12f;
    private const float DefaultDistance = 2.45f;
    private const float DefaultPitch = 0.18f;
    private const float DefaultYaw = 0.22f;

    public static void Main(string[] args)
    {
        if (TryRunHeadlessPng(args))
        {
            return;
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information)
                .AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });
        });
        var logger = loggerFactory.CreateLogger("SilkTraceStudio");
        var perf = new PathTracePerfMeter(logger);

        var compiled = ShowcaseScenes.BuildStudioShowcase();
        using var session = new PathTraceSession(compiled);
        var display = new PathTraceDisplayBuffer();
        using var worker = new PathTraceBackgroundWorker(session.Backend, display);
        var blitRate = new SilkSmoothedBlitHz();
        var orbit = CreateDefaultOrbit();
        var sample = 0;
        var blitGeneration = -1;
        var frameWidth = 0;
        var frameHeight = 0;
        var traceWidth = 0;
        var traceHeight = 0;
        var autoOrbit = false;
        var autoOrbitAngle = 0f;
        var hasBlitTexture = false;
        var movingTrace = false;
        var stillSeconds = 0f;

        logger.LogInformation(
            "SilkTraceStudio started backend={Backend}",
            session.Backend.BackendLabel);

        SilkGame.Run("SilkTraceStudio — path tracing", 1280, 720, frame =>
        {
            blitRate.Tick(frame.DeltaSeconds);

            if (frame.Width != frameWidth || frame.Height != frameHeight)
            {
                frameWidth = frame.Width;
                frameHeight = frame.Height;
            }

            if (frame.IsResetPressed())
            {
                autoOrbit = false;
                autoOrbitAngle = 0f;
                orbit = CreateDefaultOrbit();
                SoftReset(worker, ref sample);
            }

            if (frame.IsOrbitTogglePressed())
            {
                autoOrbit = !autoOrbit;
                autoOrbitAngle = orbit.Yaw;
                // Do not SoftReset/WaitForIdle — cancel via moving preview so the first orbit frame is immediate.
                sample = 0;
            }

            if (frame.IsBackendCyclePressed())
            {
                worker.WaitForIdle();
                session.CycleBackendInteractive();
                worker.ReplaceBackend(session.Backend);
                display.Invalidate(traceWidth, traceHeight);
                sample = 0;
                blitGeneration = -1;
                hasBlitTexture = false;
                logger.LogInformation("backend cycled to {Backend}", session.Backend.BackendLabel);
            }

            if (frame.IsDigitPressed(1))
            {
                SwitchBackend(worker, session, display, ref sample, ref blitGeneration, ref hasBlitTexture, traceWidth, traceHeight, PathTraceBackendKind.Ilgpu, logger);
            }
            else if (frame.IsDigitPressed(2))
            {
                SwitchBackend(worker, session, display, ref sample, ref blitGeneration, ref hasBlitTexture, traceWidth, traceHeight, PathTraceBackendKind.Vulkan, logger);
            }
            else if (frame.IsDigitPressed(3))
            {
                SwitchBackend(worker, session, display, ref sample, ref blitGeneration, ref hasBlitTexture, traceWidth, traceHeight, PathTraceBackendKind.Cpu, logger);
            }

            var cameraMoved = false;
            var dragging = !autoOrbit && frame.IsMouseButtonDown(MouseButton.Left);
            if (dragging
                && (MathF.Abs(frame.MouseDelta.X) > 1e-4f || MathF.Abs(frame.MouseDelta.Y) > 1e-4f))
            {
                orbit.AddLookDelta(frame.MouseDelta.X * MouseLookSensitivity, -frame.MouseDelta.Y * MouseLookSensitivity);
                cameraMoved = true;
            }

            if (MathF.Abs(frame.ScrollDelta) > 1e-4f)
            {
                var scale = MathF.Exp(-frame.ScrollDelta * ScrollZoomFactor);
                orbit.Distance = System.Math.Clamp(
                    orbit.Distance * scale,
                    orbit.MinDistance,
                    orbit.MaxDistance);
                cameraMoved = true;
            }

            if (autoOrbit)
            {
                autoOrbitAngle += frame.DeltaSeconds * AutoOrbitRadiansPerSecond;
                orbit.Yaw = autoOrbitAngle;
            }

            var cameraBusy = autoOrbit || cameraMoved;
            if (cameraBusy)
            {
                stillSeconds = 0f;
            }
            else
            {
                stillSeconds += frame.DeltaSeconds;
            }

            // Preview while busy; after settle, jump once to 4K and accumulate (wipe is intentional).
            movingTrace = cameraBusy || stillSeconds < MovingSettleSeconds;
            var wantW = movingTrace ? PreviewTraceWidth : SettleTraceWidth;
            var wantH = movingTrace ? PreviewTraceHeight : SettleTraceHeight;
            if (wantW != traceWidth || wantH != traceHeight)
            {
                ApplyTraceSize(
                    worker,
                    session,
                    display,
                    wantW,
                    wantH,
                    ref traceWidth,
                    ref traceHeight,
                    ref sample,
                    ref blitGeneration,
                    ref hasBlitTexture);
            }

            var aspect = traceWidth / (float)System.Math.Max(1, traceHeight);
            var camera = CameraSnapshot.LookAt(
                orbit.BuildEyePosition(),
                orbit.Target,
                Vector3.UnitY,
                orbit.FieldOfViewDegrees,
                aspect);

            var onGpu = IsRealGpu(session.Backend);
            if (cameraBusy)
            {
                // Latest pose wins. Never accumulate while the camera is moving — that freezes the view
                // for an entire multi-spp batch (the choppiness in the orbit recording).
                perf.NoteEnqueue(worker.TryEnqueueMovingPreview(
                    camera,
                    onGpu ? MovingPreviewSamplesGpu : MovingPreviewSamplesCpu));
                sample = 0;
            }
            else if (!movingTrace && sample < MaxProgressiveSamples)
            {
                perf.NoteEnqueue(worker.TryEnqueueAccumulate(
                    camera,
                    ref sample,
                    onGpu ? AccumulateSamplesPerBatchGpu : AccumulateSamplesPerBatchCpu));
            }

            var blitThisFrame = display.TryCopyFrameIfNew(ref blitGeneration, out var pixels, out var blitWidth, out var blitHeight);
            if (blitThisFrame)
            {
                frame.Blit(pixels, blitWidth, blitHeight);
                hasBlitTexture = true;
                blitRate.NoteBlit(display.DisplayedSampleCount);
            }
            else if (hasBlitTexture)
            {
                frame.RedrawBlit();
            }

            frame.SetTitle(PathTraceStatusTitle.Format(
                session.Backend,
                display.DisplayedSampleCount,
                autoOrbit,
                blitHz: blitRate.BlitHz > 0f ? blitRate.BlitHz : null,
                sppPerSecond: blitRate.SppPerSecond > 0f ? blitRate.SppPerSecond : null,
                rendering: worker.IsBusy,
                extra: $"trace {traceWidth}x{traceHeight} · dist {orbit.Distance:F2} · drag LMB · scroll zoom · Space orbit · B · 1/2/3 · R reset"));

            perf.Tick(
                frame.DeltaSeconds,
                blitRate.BlitHz,
                display.DisplayedSampleCount,
                worker.IsBusy,
                blitThisFrame,
                session.Backend.BackendLabel,
                traceWidth,
                traceHeight,
                cameraBusy);

            // Do not spin the CPU at 1500 Hz while a multi-spp GPU batch runs.
            if (worker.IsBusy && !blitThisFrame)
            {
                Thread.Sleep(8);
            }
        }, vsync: true);
    }

    private static void ApplyTraceSize(
        PathTraceBackgroundWorker worker,
        PathTraceSession session,
        PathTraceDisplayBuffer display,
        int width,
        int height,
        ref int traceWidth,
        ref int traceHeight,
        ref int sample,
        ref int blitGeneration,
        ref bool hasBlitTexture)
    {
        width = System.Math.Max(2, width) & ~1;
        height = System.Math.Max(2, height) & ~1;
        if (width == traceWidth && height == traceHeight)
        {
            return;
        }

        // Cancel in-flight work first — never WaitForIdle on a full accumulate batch.
        worker.RequestReset();
        worker.WaitForIdle();
        session.Resize(width, height);
        display.Invalidate(width, height);
        traceWidth = width;
        traceHeight = height;
        sample = 0;
        blitGeneration = -1;
        hasBlitTexture = false;
    }

    private static bool IsRealGpu(IRayTracingBackend backend) =>
        backend.BackendLabel.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
        || backend.BackendLabel.Contains("GeForce", StringComparison.OrdinalIgnoreCase)
        || backend.BackendLabel.Contains("Radeon", StringComparison.OrdinalIgnoreCase)
        || backend.BackendLabel.Contains("Arc", StringComparison.OrdinalIgnoreCase)
        || (backend.BackendLabel.StartsWith("ILGPU (", StringComparison.Ordinal)
            && !backend.BackendLabel.Contains("fallback", StringComparison.OrdinalIgnoreCase)
            && !backend.BackendLabel.Contains("CPU", StringComparison.OrdinalIgnoreCase));

    private static OrbitCameraRig CreateDefaultOrbit()
    {
        var orbit = new OrbitCameraRig
        {
            Target = ShowcaseScenes.OrbitTarget,
            Distance = DefaultDistance,
            Yaw = DefaultYaw,
            Pitch = DefaultPitch,
            MinDistance = 0.35f,
            MaxDistance = 18f,
            MinPitch = -1.2f,
            FieldOfViewDegrees = 52f,
        };
        orbit.SnapTarget(ShowcaseScenes.OrbitTarget);
        return orbit;
    }

    /// <summary>
    /// <c>--headless-png &lt;path&gt; [--width 3840] [--height 2160] [--samples 256]</c>
    /// traces the studio diorama once and writes a PNG (no window).
    /// </summary>
    private static bool TryRunHeadlessPng(string[] args)
    {
        var path = ArgValue(args, "--headless-png");
        if (path is null)
        {
            return false;
        }

        var width = ArgInt(args, "--width", 3840);
        var height = ArgInt(args, "--height", 2160);
        var samples = ArgInt(args, "--samples", 256);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        using var backend = new IlgpuRayTracingBackend { MaxTraceDepth = 6 };
        Console.WriteLine($"headless backend={backend.BackendLabel} {width}x{height} samples={samples}");
        var scene = ShowcaseScenes.BuildStudioShowcase();
        var camera = CameraSnapshot.LookAt(
            ShowcaseScenes.StudioDefaultEye,
            ShowcaseScenes.OrbitTarget,
            Vector3.UnitY,
            52f,
            width / (float)height);

        backend.ResizeAsync(width, height).GetAwaiter().GetResult();
        backend.UploadSceneAsync(scene).GetAwaiter().GetResult();
        backend.ResetAccumulation();
        backend.RenderBatchAsync(camera, 0, samples, flushToCpu: true).GetAwaiter().GetResult();
        if (!backend.Output.TryGetCpuPixels(out var pixels, out var w, out var h))
        {
            throw new InvalidOperationException("Path-trace backend produced no CPU pixels.");
        }

        var png = RgbaPng.Encode(pixels.ToArray(), w, h);
        File.WriteAllBytes(path, png);
        Console.WriteLine($"wrote {path} ({png.Length} bytes)");
        return true;
    }

    private static string? ArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static int ArgInt(string[] args, string name, int fallback)
    {
        var text = ArgValue(args, name);
        return text is not null && int.TryParse(text, out var value) && value > 0 ? value : fallback;
    }

    private static void SoftReset(PathTraceBackgroundWorker worker, ref int sample)
    {
        worker.RequestReset();
        sample = 0;
    }

    private static void SwitchBackend(
        PathTraceBackgroundWorker worker,
        PathTraceSession session,
        PathTraceDisplayBuffer display,
        ref int sample,
        ref int blitGeneration,
        ref bool hasBlitTexture,
        int traceWidth,
        int traceHeight,
        PathTraceBackendKind kind,
        ILogger logger)
    {
        worker.RequestReset();
        worker.WaitForIdle();
        session.SwitchBackend(kind);
        worker.ReplaceBackend(session.Backend);
        sample = 0;
        blitGeneration = -1;
        hasBlitTexture = false;
        if (traceWidth > 0 && traceHeight > 0)
        {
            display.Invalidate(traceWidth, traceHeight);
        }

        logger.LogInformation("backend switched to {Backend}", session.Backend.BackendLabel);
    }
}
