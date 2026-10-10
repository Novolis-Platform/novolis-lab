namespace NeuralRacing.Training;

using Novolis.Simulation.Racing.Tracks;
using Novolis.MachineLearning.Neural;

/// <summary>Published after each generation's fitness evaluation, before mutation (for visualization / logging).</summary>
public sealed record RacingGenerationEvaluated(
    int Generation,
    int TotalGenerations,
    RaceTrack Track,
    IReadOnlyList<IMutableNeuralNetwork> Population,
    double[] Fitness,
    /// <summary>Population indices sorted best → worst.</summary>
    int[] RankedIndicesBestFirst);
