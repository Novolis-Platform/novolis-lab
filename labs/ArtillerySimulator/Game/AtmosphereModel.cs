using System.Numerics;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator.Game;

/// <summary>High-plateau training atmosphere: altitude-dependent ρ, humidity, and wind shear.</summary>
internal sealed class AtmosphereModel
{
    /// <summary>Reference pressure at gun elevation (Pa) — ~2.1 km plateau.</summary>
    public double ReferencePressurePa { get; set; } = 78_800.0;

    public double ReferenceTemperatureKelvin { get; set; } = 291.15;

    /// <summary>0…1 relative humidity.</summary>
    public double RelativeHumidity { get; set; } = 0.28;

    public Vector3 SurfaceWindMetersPerSecond { get; set; } = new(14f, 0f, 6f);

    public static AtmosphereModel CreateRegionalDefault() => new();

    public double DensityKgPerM3At(float altitudeMeters)
    {
        var t = ReferenceTemperatureKelvin - 0.0065 * altitudeMeters;
        if (t < 240.0)
            t = 240.0;

        var p = StandardAtmosphere.PressureAtAltitude(
            ReferencePressurePa,
            altitudeMeters,
            ReferenceTemperatureKelvin);

        return StandardAtmosphere.DensityKgPerM3(p, t, RelativeHumidity);
    }

    public Vector3 WindAt(float altitudeMeters)
    {
        var shear = 1.0 + Math.Clamp(altitudeMeters / 9000f, 0f, 0.45f);
        return SurfaceWindMetersPerSecond * (float)shear;
    }

    public ProjectileBallisticEnvironment BallisticEnvironment(bool dragEnabled, float altitudeMeters)
    {
        var rho = dragEnabled ? DensityKgPerM3At(altitudeMeters) : 0.0;
        return new ProjectileBallisticEnvironment(9.80665, rho, WindAt(altitudeMeters));
    }

    public string SummaryLine(float sampleAltitudeMeters)
    {
        var rho = DensityKgPerM3At(sampleAltitudeMeters);
        var wind = SurfaceWindMetersPerSecond;
        var speed = wind.Length();
        return $"rho {rho:F3} kg/m3  RH {RelativeHumidity * 100:F0}%  wind {speed:F0} m/s ({wind.X:F0}, {wind.Z:F0})";
    }
}
