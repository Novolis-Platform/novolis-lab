namespace PulseStrip.Core.Ai;

using Novolis.MachineLearning.Neural;
using Novolis.Simulation.Racing.Rewards;
using Novolis.Simulation.Racing.Tracks;

public readonly record struct EvolutionaryHoverTrainerResult(
    IMutableNeuralNetwork Champion,
    double BestFitness,
    int Generations,
    IReadOnlyList<double> BestFitnessPerGeneration);
