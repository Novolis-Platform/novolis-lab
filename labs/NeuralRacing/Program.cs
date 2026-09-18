using NeuralRacing.Training;
using Novolis.Simulation.Racing.Tracks;

var settings = new EvolutionaryRacingTrainerSettings(
    Track: BuiltInTracks.MicroCircle,
    PopulationSize: 24,
    Generations: 8,
    MaxTicksPerEpisode: 400,
    RandomSeed: 42);

var result = new EvolutionaryRacingTrainer().Train(settings);
Console.WriteLine($"Generations: {result.GenerationsRun}");
Console.WriteLine($"Best fitness: {result.BestFitness:F2}");
Console.WriteLine($"Best network: {result.BestNetwork.Name}");
