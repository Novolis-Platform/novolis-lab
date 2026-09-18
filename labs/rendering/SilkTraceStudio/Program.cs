using System.Numerics;
using Novolis.Rendering.PathTrace.Demos;
using Novolis.Rendering.Presentation.Silk;
using Novolis.Simulation.View;
using Novolis.Rendering.Runtime;
using Novolis.Rendering.Presentation;

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

        SilkGame.Run("SilkTraceStudio — path tracing", 1280, 720, ctx =>
        {
            ctx.FramePresenter.ShowStatusStrip = true;
            fps.Update(ctx.DeltaSeconds);

            if (ctx.Width != frameWidth || ctx.Height != frameHeight)
            {
                frameWidth = ctx.Width;
                frameHeight = ctx.Height;
                worker.WaitForIdle();
                session.Resize(frameWidth, frameHeight);
                display.Invalidate(frameWidth, frameHeight);
                sample = 0;
            }

            if (ctx.IsResetPressed())
            {
                worker.WaitForIdle();
                session.Backend.ResetAccumulation();
                display.Invalidate(frameWidth, frameHeight);
                sample = 0;
            }

            if (ctx.IsOrbitTogglePressed())
            {
                autoOrbit = !autoOrbit;
                worker.WaitForIdle();
                session.Backend.ResetAccumulation();
                display.Invalidate(frameWidth, frameHeight);
                sample = 0;
            }

            if (ctx.IsBackendCyclePressed())
            {
                worker.WaitForIdle();
                session.CycleBackend();
                worker.ReplaceBackend(session.Backend);
                display.Invalidate(frameWidth, frameHeight);
                sample = 0;
            }

            if (ctx.IsDigitPressed(1))
            {
                SwitchBackend(worker, session, display, ref sample, frameWidth, frameHeight, PathTraceBackendKind.Ilgpu);
            }
            else if (ctx.IsDigitPressed(2))
            {
                SwitchBackend(worker, session, display, ref sample, frameWidth, frameHeight, PathTraceBackendKind.Vulkan);
            }
            else if (ctx.IsDigitPressed(3))
            {
                SwitchBackend(worker, session, display, ref sample, frameWidth, frameHeight, PathTraceBackendKind.Cpu);
            }

            if (!autoOrbit)
            {
                if (ctx.IsMouseButtonDown(MouseButton.Left))
                {
                    orbit.AddLookDelta(ctx.MouseDelta.X * MouseLookSensitivity, -ctx.MouseDelta.Y * MouseLookSensitivity);
                }

                if (MathF.Abs(ctx.ScrollDelta) > 1e-4f)
                {
                    orbit.AdjustDistance(-ctx.ScrollDelta * ScrollZoomSensitivity);
                }
            }
            else
            {
                autoOrbitAngle += ctx.DeltaSeconds * 0.35f;
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

            display.TryPresent(ctx.FramePresenter);
            ctx.SetTitle(PathTraceStatusTitle.Format(
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
