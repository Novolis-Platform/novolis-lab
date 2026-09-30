namespace NeuralRacing.Training;

using NeuralRacing.Controllers;
using Novolis.Simulation.Racing.Cars;
using Novolis.Simulation.Racing.Race;
using Novolis.Simulation.Racing.Rewards;
using Novolis.Simulation.Racing.Tracks;
using Novolis.MachineLearning.Neural;

public readonly record struct EvolutionaryRacingTrainerProgress(
    int Generation,
    int TotalGenerations,
    double GenerationBestFitness,
    double BestFitnessSoFar);
