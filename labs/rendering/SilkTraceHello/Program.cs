using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Rendering.DependencyInjection;
using Novolis.Rendering.PathTrace.Demos;
using Novolis.Rendering.Presentation.Silk;
using Novolis.Rendering.Runtime;

namespace SilkTraceHello;

internal static class Program
{
    private const int OrbitSamplesPerFrame = 16;
    private const int AccumulateSamplesPerBatch = 4;

    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddRayTracingFromEnvironment();
        var provider = services.BuildServiceProvider();
        var backend = provider.GetRequiredService<IRayTracingBackend>();
        var compiled = ShowcaseScenes.BuildHelloShowcase();
        var display = new PathTraceDisplayBuffer();
        using var worker = new PathTraceBackgroundWorker(backend, display);
        var sample = 0;
        var frameWidth = 0;
        var frameHeight = 0;
        var orbitAngle = 0f;
        var orbitEnabled = false;

        SilkGame.Run("SilkTraceHello — path tracing", 960, 540, ctx =>
        {
            if (ctx.Width != frameWidth || ctx.Height != frameHeight)
            {
                frameWidth = ctx.Width;
                frameHeight = ctx.Height;
                worker.WaitForIdle();
                backend.ResizeAsync(frameWidth, frameHeight).GetAwaiter().GetResult();
                backend.UploadSceneAsync(compiled).GetAwaiter().GetResult();
                display.Invalidate(frameWidth, frameHeight);
                sample = 0;
            }

            if (ctx.IsResetPressed())
            {
                worker.WaitForIdle();
                backend.ResetAccumulation();
                display.Invalidate(frameWidth, frameHeight);
                sample = 0;
            }

            if (ctx.IsOrbitTogglePressed())
            {
                orbitEnabled = !orbitEnabled;
                worker.WaitForIdle();
                backend.ResetAccumulation();
                display.Invalidate(frameWidth, frameHeight);
                sample = 0;
            }

            var eye = orbitEnabled
                ? ShowcaseScenes.OrbitTarget + new Vector3(
                    MathF.Sin(orbitAngle) * 2.4f,
                    0.85f,
                    MathF.Cos(orbitAngle) * 2.4f)
                : ShowcaseScenes.HelloDefaultEye;

            if (orbitEnabled)
            {
                orbitAngle += ctx.DeltaSeconds * 0.35f;
            }

            var camera = CameraSnapshot.LookAt(
                eye,
                ShowcaseScenes.OrbitTarget,
                Vector3.UnitY,
                52f,
                frameWidth / (float)Math.Max(1, frameHeight));

            if (orbitEnabled)
            {
                worker.EnqueueOrbit(camera, OrbitSamplesPerFrame);
            }
            else
            {
                worker.TryEnqueueAccumulate(camera, ref sample, AccumulateSamplesPerBatch);
            }

            display.TryPresent(ctx.FramePresenter);
            ctx.SetTitle(PathTraceStatusTitle.Format(backend, display.DisplayedSampleCount, orbitEnabled));
        });

        worker.Dispose();
        if (backend is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
