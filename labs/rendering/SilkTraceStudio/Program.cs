using System.Numerics;
using Novolis.Rendering.PathTrace.Demos;
using Novolis.Rendering.Runtime;
using Novolis.Silk;
using Novolis.Simulation.View;

namespace SilkTraceStudio;

internal static class Program
{
    // RTX-class GPUs finish these quickly; higher spp kills orbit grain from the recording.
    private const int OrbitPreviewSamples = 32;
    private const int AccumulateSamplesPerBatch = 16;
    private const int MaxProgressiveSamples = 512;
    private const float MouseLookSensitivity = 0.004f;
    private const float ScrollZoomFactor = 0.12f;
    private const float DefaultDistance = 2.6f;
    private const float DefaultPitch = 0.35f;

    public static void Main()
    {
        var compiled = ShowcaseScenes.BuildStudioShowcase();
        using var session = new PathTraceSession(compiled);
        var display = new PathTraceDisplayBuffer();
        using var worker = new PathTraceBackgroundWorker(session.Backend, display);
        var fps = new SilkSmoothedFps();
        var orbit = CreateDefaultOrbit();
        var sample = 0;
        var blitGeneration = -1;
        var frameWidth = 0;
        var frameHeight = 0;
        var traceWidth = 0;
        var traceHeight = 0;
        var autoOrbit = false;
        var autoOrbitAngle = 0f;

        SilkGame.Run("SilkTraceStudio — path tracing", 1280, 720, frame =>
        {
            fps.Update(frame.DeltaSeconds);

            if (frame.Width != frameWidth || frame.Height != frameHeight)
            {
                frameWidth = frame.Width;
                frameHeight = frame.Height;
                (traceWidth, traceHeight) = InteractiveTraceSize.Clamp(frameWidth, frameHeight);
                worker.WaitForIdle();
                session.Resize(traceWidth, traceHeight);
                display.Invalidate(traceWidth, traceHeight);
                sample = 0;
                blitGeneration = -1;
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
                SoftReset(worker, ref sample);
            }

            if (frame.IsBackendCyclePressed())
            {
                worker.WaitForIdle();
                session.CycleBackend();
                worker.ReplaceBackend(session.Backend);
                display.Invalidate(traceWidth, traceHeight);
                sample = 0;
                blitGeneration = -1;
            }

            if (frame.IsDigitPressed(1))
            {
                SwitchBackend(worker, session, display, ref sample, ref blitGeneration, traceWidth, traceHeight, PathTraceBackendKind.Ilgpu);
            }
            else if (frame.IsDigitPressed(2))
            {
                SwitchBackend(worker, session, display, ref sample, ref blitGeneration, traceWidth, traceHeight, PathTraceBackendKind.Vulkan);
            }
            else if (frame.IsDigitPressed(3))
            {
                SwitchBackend(worker, session, display, ref sample, ref blitGeneration, traceWidth, traceHeight, PathTraceBackendKind.Cpu);
            }

            var cameraMoved = false;
            if (!autoOrbit
                && frame.IsMouseButtonDown(MouseButton.Left)
                && (MathF.Abs(frame.MouseDelta.X) > 1e-4f || MathF.Abs(frame.MouseDelta.Y) > 1e-4f))
            {
                orbit.AddLookDelta(frame.MouseDelta.X * MouseLookSensitivity, -frame.MouseDelta.Y * MouseLookSensitivity);
                cameraMoved = true;
            }

            if (MathF.Abs(frame.ScrollDelta) > 1e-4f)
            {
                var scale = MathF.Exp(-frame.ScrollDelta * ScrollZoomFactor);
                orbit.Distance = Math.Clamp(
                    orbit.Distance * scale,
                    orbit.MinDistance,
                    orbit.MaxDistance);
                cameraMoved = true;
            }

            if (autoOrbit)
            {
                autoOrbitAngle += frame.DeltaSeconds * 0.35f;
                orbit.Yaw = autoOrbitAngle;
            }

            if (cameraMoved)
            {
                // Non-blocking: cancel stale GPU work instead of WaitForIdle on the UI thread.
                SoftReset(worker, ref sample);
            }

            var aspect = traceWidth / (float)Math.Max(1, traceHeight);
            var camera = CameraSnapshot.LookAt(
                orbit.BuildEyePosition(),
                orbit.Target,
                Vector3.UnitY,
                orbit.FieldOfViewDegrees,
                aspect);

            if (autoOrbit)
            {
                worker.TryEnqueueOrbitPreview(camera, OrbitPreviewSamples);
            }
            else if (sample < MaxProgressiveSamples)
            {
                worker.TryEnqueueAccumulate(camera, ref sample, AccumulateSamplesPerBatch);
            }

            // Skip redundant CPU→GL uploads when the tracer has not published a new frame.
            if (display.TryCopyFrameIfNew(ref blitGeneration, out var pixels, out var blitWidth, out var blitHeight))
            {
                frame.Blit(pixels, blitWidth, blitHeight);
            }

            frame.SetTitle(PathTraceStatusTitle.Format(
                session.Backend,
                display.DisplayedSampleCount,
                autoOrbit,
                fps.Value,
                worker.IsBusy,
                $"trace {traceWidth}x{traceHeight} · dist {orbit.Distance:F2} · drag LMB · scroll zoom · Space orbit · B · 1/2/3 · R reset"));
        });
    }

    private static OrbitCameraRig CreateDefaultOrbit()
    {
        var orbit = new OrbitCameraRig
        {
            Target = ShowcaseScenes.OrbitTarget,
            Distance = DefaultDistance,
            Yaw = 0f,
            Pitch = DefaultPitch,
            MinDistance = 0.35f,
            MaxDistance = 18f,
            MinPitch = -1.2f,
            FieldOfViewDegrees = 52f,
        };
        orbit.SnapTarget(ShowcaseScenes.OrbitTarget);
        return orbit;
    }

    private static void SoftReset(PathTraceBackgroundWorker worker, ref int sample)
    {
        // Keep the last presented frame on screen; cancel/rebuild accumulation off-thread.
        worker.RequestReset();
        sample = 0;
    }

    private static void SwitchBackend(
        PathTraceBackgroundWorker worker,
        PathTraceSession session,
        PathTraceDisplayBuffer display,
        ref int sample,
        ref int blitGeneration,
        int traceWidth,
        int traceHeight,
        PathTraceBackendKind kind)
    {
        worker.WaitForIdle();
        session.SwitchBackend(kind);
        worker.ReplaceBackend(session.Backend);
        sample = 0;
        blitGeneration = -1;
        if (traceWidth > 0 && traceHeight > 0)
        {
            display.Invalidate(traceWidth, traceHeight);
        }
    }
}
