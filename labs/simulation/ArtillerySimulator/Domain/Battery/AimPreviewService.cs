using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>Ordered / tube / PE-envelope aim arcs for HUD and fire-block checks.</summary>
internal sealed class AimPreviewService
{
    private readonly TerrainWorld _terrain;
    private readonly AtmosphereModel _atmosphere;
    private readonly BattleSession _session;
    private readonly List<IReadOnlyList<Vector3>> _dispersion = [];

    private GunModel _gun = null!;
    private ProjectileBallisticSimulation _ballistics = null!;
    private float _previewGunElev;
    private float _previewGunAz;
    private int _previewCharge = -1;
    private bool _dirty = true;
    private float _rebuildCooldown;
    private float _orderedTofSeconds;

    public AimPreviewService(TerrainWorld terrain, AtmosphereModel atmosphere, BattleSession session)
    {
        _terrain = terrain;
        _atmosphere = atmosphere;
        _session = session;
    }

    public IReadOnlyList<Vector3> Ordered { get; private set; } = [];
    public IReadOnlyList<Vector3> Tube { get; private set; } = [];
    public IReadOnlyList<IReadOnlyList<Vector3>> Dispersion => _dispersion;
    public bool IsDirty => _dirty;

    public void Bind(GunModel gun, ProjectileBallisticSimulation ballistics)
    {
        _gun = gun;
        _ballistics = ballistics;
        _dirty = true;
    }

    public void MarkDirty() => _dirty = true;

    public bool TubePreviewStale() =>
        MathF.Abs(_gun.ElevationDegrees - _previewGunElev) > 0.05f
        || MathF.Abs(_gun.AzimuthDegrees - _previewGunAz) > 0.05f
        || _gun.ChargeIndex != _previewCharge;

    /// <summary>Rebuild at most ~10 Hz unless forced by a lay/charge change.</summary>
    public void RebuildIfNeeded(CrewDrill crew, float wallSeconds = 0f)
    {
        _rebuildCooldown = MathF.Max(0f, _rebuildCooldown - wallSeconds);
        var tubeStale = TubePreviewStale();
        if (!_dirty && !tubeStale)
            return;
        if (!tubeStale && _rebuildCooldown > 0f)
            return;

        Rebuild(crew);
        _rebuildCooldown = 0.1f;
    }

    public void Rebuild(CrewDrill crew)
    {
        var elev = crew.OrderedElevation;
        var az = crew.OrderedAzimuth;
        var speed = _gun.MuzzleSpeedMps;
        Ordered = BuildPreview(elev, az, speed, windScale: 1f, coarse: false, out _orderedTofSeconds);

        var tubeDiffers = MathF.Abs(_gun.ElevationDegrees - elev) > 1.25f
            || MathF.Abs(_gun.AzimuthDegrees - az) > 1.25f;
        Tube = tubeDiffers
            ? BuildPreview(_gun.ElevationDegrees, _gun.AzimuthDegrees, speed, windScale: 1f, coarse: false, out _)
            : [];

        _dispersion.Clear();
        foreach (var s in _session.Delivery.EnvelopeCorners())
        {
            var arc = BuildPreview(
                elev + s.ElevationDegrees,
                az + s.AzimuthDegrees,
                speed * (1f + s.MuzzleFraction),
                windScale: 1f + s.WindFraction,
                coarse: true,
                out _);
            if (arc.Count >= 2)
                _dispersion.Add(arc);
        }

        _previewGunElev = _gun.ElevationDegrees;
        _previewGunAz = _gun.AzimuthDegrees;
        _previewCharge = _gun.ChargeIndex;
        _dirty = false;
    }

    public Vector3 PredictedImpact() =>
        Ordered.Count == 0 ? _terrain.GunBaseline : Ordered[^1];

    public float PredictedRangeMeters() =>
        HorizontalRange(_terrain.GunBaseline, PredictedImpact());

    /// <summary>Integrated time of flight for the ordered solution (seconds).</summary>
    public float EstimatedTofSeconds() => _orderedTofSeconds;

    public static float HorizontalRange(Vector3 origin, Vector3 point)
    {
        var dx = point.X - origin.X;
        var dz = point.Z - origin.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private IReadOnlyList<Vector3> BuildPreview(
        float elevation,
        float azimuth,
        float speed,
        float windScale,
        bool coarse,
        out float tofSeconds)
    {
        tofSeconds = 0f;
        var start = _gun.PredictState(_terrain.GunBaseline, elevation, azimuth, speed);
        // Match live round dt family: fine ~30 Hz preview, coarse ~20 Hz for PE footprint.
        var dt = coarse ? 1.0 / 20.0 : FixedStepClock.DtSeconds;
        var maxPoints = coarse ? 400 : 900;
        const double maxTimeSeconds = 120.0;
        var points = new List<Vector3>(Math.Min(Math.Max(64, maxPoints), 2_048)) { start.Position };
        var state = start;
        var finished = false;
        var drawCap = Math.Max(8, maxPoints);
        var hardSteps = Math.Max(drawCap * 4, (int)Math.Ceiling(maxTimeSeconds / dt) + 8);
        // Same contact path family as live rounds: heightfield + BVH collision world.
        var terrain = _terrain.Field;
        var collision = _terrain.Collision;
        var radius = MathF.Max(0.08f, _gun.Spec.MuzzleRadiusMeters);

        for (var step = 0; step < hardSteps; step++)
        {
            var prev = state.Position;
            var baseEnv = _atmosphere.BallisticEnvironment(state.Position.Y);
            var env = windScale == 1f
                ? baseEnv
                : new ProjectileBallisticEnvironment(
                    baseEnv.GravityMetersPerSecondSquared,
                    baseEnv.AirDensityKgPerM3,
                    baseEnv.WindMetersPerSecond * windScale,
                    baseEnv.TemperatureKelvin,
                    baseEnv.LatitudeDegrees);
            state = _ballistics.Step(state, dt, env);
            var p = state.Position;
            tofSeconds = (float)state.TimeSeconds;

            if (terrain.TrySegmentLeavesRange(prev, p, out var boundaryHit, out _))
            {
                points.Add(terrain.ProjectOntoSurface(boundaryHit));
                finished = true;
                break;
            }

            if (!terrain.IsInside(p.X, p.Z))
            {
                points.Add(terrain.ProjectOntoSurface(p));
                finished = true;
                break;
            }

            // Prefer collision mesh contact (same as BallisticTrajectoryRunner) then fine field.
            var sphere = new Sphere(prev, radius);
            var displacement = p - prev;
            if (displacement.LengthSquared() > 1e-8f
                && BallisticsQueries.SweepProjectileSphere(collision, in sphere, displacement, out var meshHit))
            {
                points.Add(meshHit.Point);
                finished = true;
                break;
            }

            if (terrain.TryHeightfieldContact(p, radius))
            {
                points.Add(p);
                finished = true;
                break;
            }

            if (points.Count < drawCap)
                points.Add(p);
            else if (step % 3 == 0)
                points[^1] = p;
        }

        if (!finished && points.Count >= 1)
            points[^1] = terrain.ProjectOntoSurface(state.Position);

        return points;
    }
}
