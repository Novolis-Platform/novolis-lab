namespace PulseStrip.Core;

/// <summary>Full-throttle demo AI used when no brain is available.</summary>
public sealed class FullThrottleHoverController : IHoverController
{
    public string Name { get; }

    public FullThrottleHoverController(string name = "ThrottleBot")
    {
        Name = name;
    }

    public HoverControlDecision Decide(in HoverObservation observation)
    {
        var sensors = observation.Sensors.Values;
        // Steer away from nearer wall (left vs right rays).
        var left = sensors.Length > 0 ? sensors[0] : 0;
        var right = sensors.Length > 6 ? sensors[6] : 0;
        var steer = Math.Clamp(right - left, -1.0, 1.0);
        var ahead = sensors.Length > 3 ? sensors[3] : 0;
        var brake = ahead > 0.75 ? 0.6 : 0.0;
        var throttle = 1.0 - brake;
        var boost = ahead < 0.35 && observation.State.BoostFuel > 0.4 ? 1.0 : 0.0;
        var fire = observation.State.WeaponAmmo > 0 && observation.Standing.Position > 1;
        return new HoverControlDecision(steer, throttle, brake, boost, fire);
    }
}
