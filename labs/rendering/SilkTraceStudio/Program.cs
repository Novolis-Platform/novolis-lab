using System.Numerics;
using Novolis.Rendering.PathTrace.Demos;
using Novolis.Rendering.Runtime;
using Novolis.Silk;
using Novolis.Simulation.View;

namespace SilkTraceStudio;

internal static class Program
{
    private const int OrbitSamplesPerFrame = 16;
    private const int AccumulateSamplesPerBatch = 4;
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
        var autoOrbit = false;
        var autoOrbitAngle = 0f;

        SilkGame.Run("SilkTraceStudio — path tracing", 1280, 720, frame =>
        {
            fps.Update(frame.DeltaSeconds);

            if (frame.Width != frameWidth || frame.Height != frameHeight)
            {
                frameWidth = frame.Width;
                frameHeight = frame.Height;
                worker.WaitForIdle();
                session.Resize(frameWidth, frameHeight);
                display.Invalidate(frameWidth, frameHeight);
                sample = 0;
            }

            if (frame.IsResetPressed())
            {
                worker.WaitForIdle();
                session.Backend.ResetAccumulation();
                display.Invalidate(frameWidth, frameHeight);
                sample = 0;
            }

            if (frame.IsOrbitTogglePressed())
            {
                autoOrbit = !autoOrbit;
                worker.WaitForIdle();
                session.Backend.ResetAccumulation();
                display.Invalidate(frameWidth, frameHeight);
                sample = 0;
            }

            if (frame.IsBackendCyclePressed())
            {
                worker.WaitForIdle();
                session.CycleBackend();
                worker.ReplaceBackend(session.Backend);
                display.Invalidate(frameWidth, frameHeight);
                sample = 0;
            }

            if (frame.IsDigitPressed(1))
            {
                SwitchBackend(worker, session, display, ref sample, frameWidth, frameHeight, PathTraceBackendKind.Ilgpu);
            }
            else if (frame.IsDigitPressed(2))
            {
                SwitchBackend(worker, session, display, ref sample, frameWidth, frameHeight, PathTraceBackendKind.Vulkan);
            }
            else if (frame.IsDigitPressed(3))
            {
                SwitchBackend(worker, session, display, ref sample, frameWidth, frameHeight, PathTraceBackendKind.Cpu);
            }

            if (!autoOrbit)
            {
                if (frame.IsMouseButtonDown(MouseButton.Left))
                {
                    orbit.AddLookDelta(frame.MouseDelta.X * MouseLookSensitivity, -frame.MouseDelta.Y * MouseLookSensitivity);
                }

                if (MathF.Abs(frame.ScrollDelta) > 1e-4f)
                {
                    orbit.AdjustDistance(-frame.ScrollDelta * ScrollZoomSensitivity);
                }
            }
            else
            {
                autoOrbitAngle += frame.DeltaSeconds * 0.35f;
                orbit.Yaw = autoOrbitAngle;
            }

            var aspect = frameWidth / (float)Math.Max(1, frameHeight);
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
                "drag LMB · scroll zoom · Space auto-orbit · B cycle backend · 1/2/3 ILGPU/Vulkan/CPU · R reset"));
        });
    }

    private static void SwitchBackend(
        PathTraceBackgroundWorker worker,
        PathTraceSession session,
        PathTraceDisplayBuffer display,
        ref int sample,
        int frameWidth,
        int frameHeight,
        PathTraceBackendKind kind)
    {
        worker.WaitForIdle();
        session.SwitchBackend(kind);
        worker.ReplaceBackend(session.Backend);
        if (frameWidth > 0 && frameHeight > 0)
        {
            display.Invalidate(frameWidth, frameHeight);
        }

        sample = 0;
    }
}
