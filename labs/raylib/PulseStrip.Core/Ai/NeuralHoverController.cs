namespace PulseStrip.Core.Ai;

using Novolis.MachineLearning.Neural;

/// <summary>Maps dense-network outputs to hover controls (10 sensors → 5 controls).</summary>
public sealed class NeuralHoverController : IHoverController
{
    private readonly INeuralNetwork _network;

    public NeuralHoverController(INeuralNetwork network)
    {
        _network = network;
    }

    public string Name => _network.Name;

    public HoverControlDecision Decide(in HoverObservation observation)
    {
        var inputs = observation.Sensors.Values;
        if (inputs.Length < HoverRaceSimulation.SensorInputSize)
            throw new InvalidOperationException($"Expected {HoverRaceSimulation.SensorInputSize} sensors.");

        var eval = _network.Evaluate(inputs.AsSpan(0, HoverRaceSimulation.SensorInputSize));
        var output = eval.Output;
        var steering = Math.Clamp(output[0], -1.0, 1.0);
        var throttle = Math.Clamp(output.Length > 1 ? output[1] : 1.0, 0.0, 1.0);
        var brake = Math.Clamp(output.Length > 2 ? output[2] : 0.0, 0.0, 1.0);
        var boost = Math.Clamp(output.Length > 3 ? output[3] : 0.0, 0.0, 1.0);
        var fire = (output.Length > 4 ? output[4] : 0.0) > 0.55 && observation.State.WeaponAmmo > 0;
        return new HoverControlDecision(steering, throttle, brake, boost, fire);
    }
}
