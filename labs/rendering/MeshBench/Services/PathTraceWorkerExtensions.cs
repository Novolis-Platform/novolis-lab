using System.Numerics;
using Novolis.Rendering.PathTrace.Demos;
using Novolis.Rendering.Presentation.Abstractions;
using Novolis.Simulation.View;
using Novolis.Rendering.Runtime;

namespace MeshBench.Services;

internal static class PathTraceWorkerExtensions
{
    public static Task WaitForIdleAsync(this PathTraceBackgroundWorker worker) =>
        Task.Run(worker.WaitForIdle);
}
