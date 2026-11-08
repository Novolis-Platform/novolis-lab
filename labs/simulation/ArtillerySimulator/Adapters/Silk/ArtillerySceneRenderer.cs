using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>World + HUD draw for the playing frame (Silk host stays thin).</summary>
internal sealed class ArtillerySceneRenderer : IDisposable
{
    private static readonly Rgba32 ClearSky = new(18, 28, 42, 255);

    private readonly SilkWireBatch _wires = new();
    private readonly SmoothedFps _fps = new();
    private readonly SkyDomeCache _sky = new();
    private bool _disposed;

    public SilkWireBatch Wires => _wires;
    public float Fps => _fps.Value;

    public void DrawPlaying(SilkFrame frame, FrameViewModel vm, FrameProfiler? profiler)
    {
        frame.Clear(ClearSky);
        var cam = vm.Camera.Build(frame.DeltaSeconds);
        var aspect = frame.Width / (float)Math.Max(1, frame.Height);
        var view = KristiansandGeoref.CreateMapView(cam.Position, cam.Target, cam.Up);
        var extent = MathF.Max(vm.Terrain.EastMeters, vm.Terrain.NorthMeters);
        var near = 2f;
        var far = MathF.Max(extent * 6f, Vector3.Distance(cam.Position, cam.Target) * 12f);
        far = MathF.Max(far, 250_000f);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(
            cam.FieldOfViewDegrees * (MathF.PI / 180f),
            aspect,
            near,
            far);
        var mvp = view * proj;

        _sky.Ensure(frame, cam.Position, vm.Terrain.GunBaseline, extent);
        _sky.DrawIfReady(frame, mvp);

        if (profiler is not null)
            profiler.Time("draw.static", () => vm.Terrain.DrawStatic(frame, mvp));
        else
            vm.Terrain.DrawStatic(frame, mvp);

        _wires.Clear();
        void BuildDynamic() => BattleWireComposer.Compose(_wires, vm, cam.Position);

        if (profiler is not null)
            profiler.Time("draw.dynamic.build", BuildDynamic);
        else
            BuildDynamic();

        if (profiler is not null)
            profiler.Time("draw.dynamic.upload", () => frame.DrawWorldLines(_wires.AsSpan(), mvp));
        else
            frame.DrawWorldLines(_wires.AsSpan(), mvp);

        frame.DrawScreenGrade(vm.FxTime, intensity: 0.92f);

        _fps.Update(frame.DeltaSeconds);
        void DrawHud() => HudFrameComposer.Draw(frame, vm, _fps.Value);

        if (profiler is not null)
            profiler.Time("draw.hud", DrawHud);
        else
            DrawHud();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _sky.Dispose();
    }
}
