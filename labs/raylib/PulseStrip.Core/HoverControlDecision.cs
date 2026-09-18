namespace PulseStrip.Core;

/// <summary>Per-tick control outputs for a hover craft (player or ML).</summary>
public readonly record struct HoverControlDecision(
    double Steering,
    double Throttle,
    double Brake,
    double Boost,
    bool Fire);
