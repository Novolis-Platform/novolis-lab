using System.Numerics;
using Novolis.Rendering.PathTrace.Demos;
using Novolis.Rendering.Runtime;
using Novolis.Silk;
using Novolis.Simulation.View;

namespace SilkTraceStudio;

internal static class Program
{
    private const int OrbitSamplesPerFrame = 24;
    private const int AccumulateSamplesPerBatch = 24;
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
            }

            if (frame.IsResetPressed())
            {
                autoOrbit = false;
                autoOrbitAngle = 0f;
                orbit = CreateDefaultOrbit();
                ResetTrace(worker, session, display, ref sample, traceWidth, traceHeight);
            }

            if (frame.IsOrbitTogglePressed())
            {
                autoOrbit = !autoOrbit;
                autoOrbitAngle = orbit.Yaw;
                ResetTrace(worker, session, display, ref sample, traceWidth, traceHeight);
            }

            if (frame.IsBackendCyclePressed())
            {
                worker.WaitForIdle();
                session.CycleBackend();
                worker.ReplaceBackend(session.Backend);
                display.Invalidate(traceWidth, traceHeight);
                sample = 0;
            }

            if (frame.IsDigitPressed(1))
            {
                SwitchBackend(worker, session, display, ref sample, traceWidth, traceHeight, PathTraceBackendKind.Ilgpu);
            }
            else if (frame.IsDigitPressed(2))
            {
                SwitchBackend(worker, session, display, ref sample, traceWidth, traceHeight, PathTraceBackendKind.Vulkan);
            }
            else if (frame.IsDigitPressed(3))
            {
                SwitchBackend(worker, session, display, ref sample, traceWidth, traceHeight, PathTraceBackendKind.Cpu);
            }

            var cameraMoved = false;
            if (!autoOrbit
                && frame.IsMouseButtonDown(MouseButton.Left)
                && (MathF.Abs(frame.MouseDelta.X) > 1e-4f || MathF.Abs(frame.MouseDelta.Y) > 1e-4f))
            {
                orbit.AddLookDelta(frame.MouseDelta.X * MouseLookSensitivity, -frame.MouseDelta.Y * MouseLookSensitivity);
                cameraMoved = true;
            }

            // Zoom works in both free-look and auto-orbit (dolly toward target).
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
                ResetTrace(worker, session, display, ref sample, traceWidth, traceHeight);
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
                worker.EnqueueOrbit(camera, OrbitSamplesPerFrame);
            }
            else
            {
                worker.TryEnqueueAccumulate(camera, ref sample, AccumulateSamplesPerBatch);
            }

            if (display.TryCopyFrame(out var pixels, out var blitWidth, out var blitHeight))
            {
                frame.Blit(pixels, blitWidth, blitHeight);
            }

            frame.SetTitle(PathTraceStatusTitle.Format(
                session.Backend,
                display.DisplayedSampleCount,
                autoOrbit,
                fps.Value,
                worker.IsBusy,
                $"trace {traceWidth}x{traceHeight} · dist {orbit.Distance:F2} · drag LMB · scroll zoom · Space orbit · B · 1/2/3 · R reset view"));
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
            // Scene is meter-scale; Simulation defaults (80…24000) are for CAD/world units.
            MinDistance = 0.35f,
            MaxDistance = 18f,
            MinPitch = -1.2f,
            FieldOfViewDegrees = 52f,
        };
        orbit.SnapTarget(ShowcaseScenes.OrbitTarget);
        return orbit;
    }

    private static void ResetTrace(
        PathTraceBackgroundWorker worker,
        PathTraceSession session,
        PathTraceDisplayBuffer display,
        ref int sample,
        int traceWidth,
        int traceHeight)
    {
        worker.WaitForIdle();
        session.Backend.ResetAccumulation();
        if (traceWidth > 0 && traceHeight > 0)
        {
            display.Invalidate(traceWidth, traceHeight);
        }

        sample = 0;
    }

    private static void SwitchBackend(
        PathTraceBackgroundWorker worker,
        PathTraceSession session,
        PathTraceDisplayBuffer display,
        ref int sample,
        int traceWidth,
        int traceHeight,
        PathTraceBackendKind kind)
    {
        worker.WaitForIdle();
        session.SwitchBackend(kind);
        worker.ReplaceBackend(session.Backend);
        if (traceWidth > 0 && traceHeight > 0)
        {
            display.Invalidate(traceWidth, traceHeight);
        }

        sample = 0;
    }
}
