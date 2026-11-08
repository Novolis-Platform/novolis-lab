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
    private const float ScrollZoomSensitivity = 0.15f;

    public static void Main()
    {
        var compiled = ShowcaseScenes.BuildStudioShowcase();
        using var session = new PathTraceSession(compiled);
        var display = new PathTraceDisplayBuffer();
        using var worker = new PathTraceBackgroundWorker(session.Backend, display);
        var fps = new SilkSmoothedFps();
        var orbit = new OrbitCameraRig { Target = ShowcaseScenes.OrbitTarget, Distance = 2.6f };
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
                ResetTrace(worker, session, display, ref sample, traceWidth, traceHeight);
            }

            if (frame.IsOrbitTogglePressed())
            {
                autoOrbit = !autoOrbit;
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
            if (!autoOrbit)
            {
                if (frame.IsMouseButtonDown(MouseButton.Left)
                    && (MathF.Abs(frame.MouseDelta.X) > 1e-4f || MathF.Abs(frame.MouseDelta.Y) > 1e-4f))
                {
                    orbit.AddLookDelta(frame.MouseDelta.X * MouseLookSensitivity, -frame.MouseDelta.Y * MouseLookSensitivity);
                    cameraMoved = true;
                }

                if (MathF.Abs(frame.ScrollDelta) > 1e-4f)
                {
                    orbit.AdjustDistance(-frame.ScrollDelta * ScrollZoomSensitivity);
                    cameraMoved = true;
                }
            }
            else
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
                $"trace {traceWidth}x{traceHeight} · drag LMB · scroll zoom · Space auto-orbit · B cycle · 1/2/3 · R reset"));
        });
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
