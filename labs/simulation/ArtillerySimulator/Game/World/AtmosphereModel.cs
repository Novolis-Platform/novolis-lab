using System.Numerics;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator.Game;

/// <summary>
/// Sea-level ISA reduced to the projectile's absolute altitude, plus a scenario wind.
/// The wind is 4 m/s from the west (+X). It is not a sounding.
/// </summary>
internal sealed class AtmosphereModel
{
    /// <summary>Sea-level reference pressure (Pa). Altitude passed in is meters above this level.</summary>
    public double ReferencePressurePa { get; set; } = 101_325.0;

    public double ReferenceTemperatureKelvin { get; set; } = 288.15;

    /// <summary>0…1 relative humidity. Scenario value for the valley, not a measured sounding.</summary>
    public double RelativeHumidity { get; set; } = 0.55;

    public Vector3 SurfaceWindMetersPerSecond { get; set; } = new(4f, 0f, 1f);

    public static AtmosphereModel CreateRegionalDefault() => new();

    public double DensityKgPerM3At(float altitudeMeters)
    {
        var t = TemperatureAt(altitudeMeters);
        var p = StandardAtmosphere.PressureAtAltitude(
            ReferencePressurePa,
            Math.Max(0.0, altitudeMeters),
            ReferenceTemperatureKelvin);

        return StandardAtmosphere.DensityKgPerM3(p, t, RelativeHumidity);
    }

    public double TemperatureAt(float altitudeMeters)
    {
        var t = ReferenceTemperatureKelvin - 0.0065 * Math.Max(0.0, altitudeMeters);
        return Math.Max(t, 216.65);
    }

    public Vector3 WindAt(float altitudeMeters)
    {
        var shear = 1.0 + Math.Clamp(altitudeMeters / 9000f, 0f, 0.45f);
        return SurfaceWindMetersPerSecond * (float)shear;
    }

    public ProjectileBallisticEnvironment BallisticEnvironment(bool dragEnabled, float altitudeMeters)
    {
        var rho = dragEnabled ? DensityKgPerM3At(altitudeMeters) : 0.0;
        return new ProjectileBallisticEnvironment(
            9.80665,
            rho,
            WindAt(altitudeMeters),
            TemperatureAt(altitudeMeters),
            KristiansandHeightSampler.MeanLatitudeDegrees);
    }

    public string SummaryLine(float sampleAltitudeMeters)
    {
        var rho = DensityKgPerM3At(sampleAltitudeMeters);
        var wind = SurfaceWindMetersPerSecond;
        var speed = wind.Length();
        return $"ISA {rho:F3} kg/m3  {TemperatureAt(sampleAltitudeMeters):F0} K  wind {speed:F0} m/s  {KristiansandHeightSampler.MeanLatitudeDegrees:F1} N";
    }
}
