namespace NeuralRacing.Controllers;

using Novolis.MachineLearning.Neural;
using Novolis.Simulation.Racing.Cars;

public interface INeuralRaceCarController : IRaceCarController
{
    INeuralNetwork Network { get; }
    NetworkEvaluation? LastEvaluation { get; }
}
