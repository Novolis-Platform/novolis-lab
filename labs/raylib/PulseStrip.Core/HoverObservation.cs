namespace PulseStrip.Core;

using Novolis.Simulation.Racing.Cars;
using Novolis.Simulation.Racing.Progress;
using Novolis.Simulation.Racing.Sensors;

/// <summary>Observation fed to hover controllers each tick.</summary>
public readonly record struct HoverObservation(
    int CraftIndex,
    HoverCraftState State,
    SensorReading Sensors,
    TrackProgressSample Progress,
    RaceStanding Standing);
