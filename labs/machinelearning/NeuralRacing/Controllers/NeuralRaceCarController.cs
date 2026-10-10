namespace NeuralRacing.Controllers;

using Novolis.MachineLearning.Neural;
using Novolis.Simulation.Racing.Cars;

public sealed class NeuralRaceCarController : INeuralRaceCarController
{
    private readonly INeuralNetwork _network;

    public NeuralRaceCarController(INeuralNetwork network)
    {
        _network = network;
    }

    public string Name => _network.Name;
    public CarVisualStyle VisualStyle { get; init; } = new("▶", "white");
    public INeuralNetwork Network => _network;
    public NetworkEvaluation? LastEvaluation { get; private set; }

    public CarControlDecision Decide(in CarObservation observation)
    {
        var inputs = observation.Sensors.Values;
        var eval = _network.Evaluate(inputs.AsSpan());
        LastEvaluation = eval;
        var output = eval.Output;
        double steering = Math.Clamp(output[0], -1.0, 1.0);
        double throttle = Math.Clamp(output[1], 0.0, 1.0);
        double brake = Math.Clamp(output.Length > 2 ? output[2] : 0.0, 0.0, 1.0);
        return new CarControlDecision(steering, throttle, brake);
    }
}
