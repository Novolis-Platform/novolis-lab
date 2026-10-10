namespace NeuralRacing.Training;

using Novolis.MachineLearning.Neural;

public sealed record EvolutionaryRacingTrainerResult(
    IMutableNeuralNetwork BestNetwork,
    double BestFitness,
    int GenerationsRun,
    IReadOnlyList<double> BestFitnessPerGeneration);
