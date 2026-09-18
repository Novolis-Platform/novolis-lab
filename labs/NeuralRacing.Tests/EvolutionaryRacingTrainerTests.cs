namespace NeuralRacing.Tests;

using NeuralRacing.Training;
using Novolis.Simulation.Racing.Tracks;

using TUnit.Assertions;

public sealed class EvolutionaryRacingTrainerTests
{
    [Test]
    public async Task Train_SmallRun_CompletesWithChampion()
    {
        var settings = new EvolutionaryRacingTrainerSettings(
            Track: BuiltInTracks.Circle,
            PopulationSize: 6,
            Generations: 3,
            MaxTicksPerEpisode: 1200,
            EliteCount: 1,
            TournamentSize: 2,
            RandomSeed: 42);

        var trainer = new EvolutionaryRacingTrainer();
        var result = trainer.Train(settings);

        await Assert.That(result.BestFitness).IsNotEqualTo(double.NegativeInfinity);
        await Assert.That(result.BestNetwork.InputSize).IsEqualTo(EvolutionaryRacingTrainer.RacingInputSize);
        await Assert.That(result.BestNetwork.OutputSize).IsEqualTo(EvolutionaryRacingTrainer.RacingOutputSize);
        await Assert.That(result.GenerationsRun).IsEqualTo(3);
        await Assert.That(result.BestFitnessPerGeneration.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Train_AfterGenerationEvaluated_InvokedOncePerGeneration()
    {
        var calls = 0;
        var settings = new EvolutionaryRacingTrainerSettings(
            Track: BuiltInTracks.Circle,
            PopulationSize: 4,
            Generations: 3,
            MaxTicksPerEpisode: 400,
            EliteCount: 1,
            TournamentSize: 2,
            RandomSeed: 7,
            AfterGenerationEvaluated: _ => calls++);

        new EvolutionaryRacingTrainer().Train(settings);
        await Assert.That(calls).IsEqualTo(3);
    }
}
