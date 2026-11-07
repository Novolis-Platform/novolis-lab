using System.Globalization;
using System.Numerics;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>
/// Kristiansand coastal atmosphere for the ballistic integrator — not a METAR feed and not a HUD decoration.
/// Wind, QNH, temperature, and humidity drift slowly inside ordinary Skagerrak ranges; the same state drives
/// aim previews, dispersion, and every in-flight physics step via <see cref="BallisticEnvironment"/>.
/// </summary>
internal sealed class AtmosphereModel
{
    /// <summary>Velocity heading (degrees, toward) for a prevailing WSW surface flow — air from ~250°.</summary>
    private const float PrevailingTowardDegrees = 70f;

    /// <summary>Half-width around the prevailing heading. Keeps the wind in one coastal sector (no flips).</summary>
    private const float HeadingHalfSpanDegrees = 32f;

    /// <summary>Maximum heading change while the clock runs (degrees per minute of battle time).</summary>
    private const float MaxTurnDegreesPerMinute = 1.15f;

    private const float MinSpeedMps = 2.2f;
    private const float MaxSpeedMps = 7.2f;

    private float _battleSeconds;
    private float _headingTowardDegrees = PrevailingTowardDegrees;
    private float _speedMps = 4.5f;
    private Vector3 _lastPublishedWind = new(4.2f, 0f, 1.5f);
    private double _lastPublishedKelvin = 281.15;
    private bool _aimInvalid;

    /// <summary>Sea-level reference pressure (Pa). Altitude passed in is meters above this level.</summary>
    public double ReferencePressurePa { get; private set; } = 101_000.0;

    public double ReferenceTemperatureKelvin { get; private set; } = 281.15;

    /// <summary>0…1 relative humidity. Coastal valley scenario, not a measured sounding.</summary>
    public double RelativeHumidity { get; private set; } = 0.72;

    /// <summary>Horizontal surface wind velocity (m/s). +X east, +Z north — where the air is going.</summary>
    public Vector3 SurfaceWindMetersPerSecond { get; private set; } = new(4.2f, 0f, 1.5f);

    public float BattleAgeSeconds => _battleSeconds;

    /// <summary>Meteorological "from" direction in degrees (0 = north, 90 = east).</summary>
    public float WindFromDegrees
    {
        get
        {
            var from = _headingTowardDegrees + 180f;
            while (from >= 360f)
                from -= 360f;
            while (from < 0f)
                from += 360f;
            return from;
        }
    }

    /// <summary>Velocity heading in degrees (where the wind blows toward).</summary>
    public float WindTowardDegrees => _headingTowardDegrees;

    public float WindSpeedMetersPerSecond => _speedMps;

    public static AtmosphereModel CreateRegionalDefault()
    {
        var model = new AtmosphereModel();
        model.PublishState(force: true);
        return model;
    }

    /// <summary>
    /// Advance the coastal met clock by battle seconds. Same clock feeds density, temperature, humidity,
    /// and wind into <see cref="BallisticEnvironment"/> — no parallel cosmetic met.
    /// </summary>
    public void Advance(float battleSeconds)
    {
        if (battleSeconds <= 0f)
            return;

        _battleSeconds += battleSeconds;
        var t = _battleSeconds;

        // Gentle multi-period coastal breeze (Beaufort 2–4). No calm reverse, no gale spikes.
        var targetSpeed = 4.55f
            + 1.35f * MathF.Sin(Tau(t / 780f))
            + 0.55f * MathF.Sin(Tau(t / 210f) + 1.17f)
            + 0.22f * MathF.Sin(Tau(t / 53f) + 0.41f);
        targetSpeed = Math.Clamp(targetSpeed, MinSpeedMps, MaxSpeedMps);

        // Slow heading wander inside the WSW–WNW coastal sector; rate-limited so it never flips.
        var targetHeading = PrevailingTowardDegrees
            + 20f * MathF.Sin(Tau(t / 1260f))
            + 9f * MathF.Sin(Tau(t / 430f) + 2.05f)
            + 3f * MathF.Sin(Tau(t / 95f) + 0.7f);
        targetHeading = Math.Clamp(
            targetHeading,
            PrevailingTowardDegrees - HeadingHalfSpanDegrees,
            PrevailingTowardDegrees + HeadingHalfSpanDegrees);

        var maxTurn = MaxTurnDegreesPerMinute * (battleSeconds / 60f);
        _headingTowardDegrees = MoveAngleDegrees(_headingTowardDegrees, targetHeading, maxTurn);

        // Speed eases; never crosses zero (no reverse).
        var speedBlend = Math.Clamp(battleSeconds / 18f, 0.02f, 0.35f);
        _speedMps = Math.Clamp(
            _speedMps + (targetSpeed - _speedMps) * speedBlend,
            MinSpeedMps,
            MaxSpeedMps);

        // Cool Skagerrak day (~8 °C) with a soft diurnal and shorter coastal pulse.
        ReferenceTemperatureKelvin = 281.15
            + 2.1 * Math.Sin(Tau(t / 3600.0))
            + 0.45 * Math.Sin(Tau(t / 520.0) + 0.9);

        // QNH stays in a quiet coastal band (~1004–1016 hPa).
        var qnhHpa = 1010.0
            + 4.0 * Math.Sin(Tau(t / 2400.0) + 0.3)
            + 1.2 * Math.Sin(Tau(t / 640.0) + 1.4);
        ReferencePressurePa = Math.Clamp(qnhHpa, 1004.0, 1016.0) * 100.0;

        // Moist coastal air; stays humid without fog extremes.
        RelativeHumidity = Math.Clamp(
            0.72
            + 0.07 * Math.Sin(Tau(t / 1500.0) + 0.5)
            + 0.03 * Math.Sin(Tau(t / 280.0)),
            0.58,
            0.86);

        PublishState(force: false);
    }

    /// <summary>True once when met drifted enough that the aim MPI should be rebuilt.</summary>
    public bool ConsumeAimInvalidation()
    {
        if (!_aimInvalid)
            return false;
        _aimInvalid = false;
        return true;
    }

    public double DensityKgPerM3At(float altitudeMeters)
    {
        var temperature = TemperatureAt(altitudeMeters);
        var pressure = StandardAtmosphere.PressureAtAltitude(
            ReferencePressurePa,
            Math.Max(0.0, altitudeMeters),
            ReferenceTemperatureKelvin);
        return StandardAtmosphere.DensityKgPerM3(pressure, temperature, RelativeHumidity);
    }

    public double TemperatureAt(float altitudeMeters)
    {
        var temperature = ReferenceTemperatureKelvin - 0.0065 * Math.Max(0.0, altitudeMeters);
        return Math.Max(temperature, 216.65);
    }

    public Vector3 WindAt(float altitudeMeters)
    {
        // Mild positive shear with height — coastal PBL, not a jet-stream flip.
        var shear = 1.0 + Math.Clamp(altitudeMeters / 9000f, 0f, 0.35f);
        return SurfaceWindMetersPerSecond * (float)shear;
    }

    /// <summary>Environment sampled by the projectile stepper and aim previews — same live met.</summary>
    public ProjectileBallisticEnvironment BallisticEnvironment(float altitudeMeters) =>
        new(
            9.80665,
            DensityKgPerM3At(altitudeMeters),
            WindAt(altitudeMeters),
            TemperatureAt(altitudeMeters),
            KristiansandHeightSampler.MeanLatitudeDegrees);

    public string SummaryLine(float sampleAltitudeMeters)
    {
        var rho = DensityKgPerM3At(sampleAltitudeMeters);
        var tc = TemperatureAt(sampleAltitudeMeters) - 273.15;
        var qnh = ReferencePressurePa / 100.0;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"KRS coastal  ρ={rho:F3} kg/m³  {tc:F1}°C  RH={RelativeHumidity * 100:F0}%  QNH={qnh:F0}  wind {_speedMps:F1} m/s from {WindFromDegrees:F0}°  {KristiansandHeightSampler.MeanLatitudeDegrees:F1} N  t={_battleSeconds:F0}s");
    }

    private void PublishState(bool force)
    {
        var rad = _headingTowardDegrees * (MathF.PI / 180f);
        var wind = new Vector3(
            _speedMps * MathF.Cos(rad),
            0f,
            _speedMps * MathF.Sin(rad));
        SurfaceWindMetersPerSecond = wind;

        if (force)
        {
            _lastPublishedWind = wind;
            _lastPublishedKelvin = ReferenceTemperatureKelvin;
            return;
        }

        var windDelta = Vector3.Distance(wind, _lastPublishedWind);
        var tempDelta = Math.Abs(ReferenceTemperatureKelvin - _lastPublishedKelvin);
        if (windDelta < 0.12f && tempDelta < 0.25)
            return;

        _lastPublishedWind = wind;
        _lastPublishedKelvin = ReferenceTemperatureKelvin;
        _aimInvalid = true;
    }

    private static float Tau(float cycles) => cycles * (MathF.PI * 2f);

    private static double Tau(double cycles) => cycles * (Math.PI * 2.0);

    private static float MoveAngleDegrees(float current, float target, float maxDelta)
    {
        var delta = target - current;
        while (delta > 180f)
            delta -= 360f;
        while (delta < -180f)
            delta += 360f;
        if (MathF.Abs(delta) <= maxDelta)
            return target;
        return current + MathF.Sign(delta) * maxDelta;
    }
}
