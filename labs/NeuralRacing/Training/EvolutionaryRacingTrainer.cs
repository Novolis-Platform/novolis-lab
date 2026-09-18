namespace NeuralRacing.Training;

using NeuralRacing.Controllers;
using Novolis.Simulation.Racing.Cars;
using Novolis.Simulation.Racing.Race;
using Novolis.Simulation.Racing.Rewards;
using Novolis.Simulation.Racing.Tracks;
using Novolis.MachineLearning.Neural;

/// <summary>
/// Evolves <see cref="DenseNetwork"/> racing agents in <see cref="RaceSimulation"/> using
/// cumulative <see cref="IRewardModel"/> fitness (spec: reward/fitness separation + mutation harness).
/// </summary>
public sealed class EvolutionaryRacingTrainer
{
    public const int RacingInputSize = 10;
    public const int RacingOutputSize = 3;

    private readonly ITrackBuilder _trackBuilder;

    public EvolutionaryRacingTrainer(ITrackBuilder? trackBuilder = null)
    {
        _trackBuilder = trackBuilder ?? new TrackBuilder();
    }

    public EvolutionaryRacingTrainerResult Train(
        EvolutionaryRacingTrainerSettings settings,
        IProgress<EvolutionaryRacingTrainerProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        settings.Validate();
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.PopulationSize, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.Generations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.MaxTicksPerEpisode, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.TournamentSize, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.EliteCount, 1);
        if (settings.EliteCount >= settings.PopulationSize)
            throw new ArgumentException("Elite count must be smaller than population size.");

        var random = settings.RandomSeed == 0 ? new Random() : new Random(settings.RandomSeed);
        var track = _trackBuilder.Build(settings.Track);
        var holdoutTrack = settings.JointHoldoutFitness && settings.HoldoutTrack is not null
            ? _trackBuilder.Build(settings.HoldoutTrack)
            : null;
        var rewardModel = settings.RewardModelOrDefault;
        var mutation = settings.MutationOrDefault;
        var hidden = settings.HiddenLayerSizesOrDefault;

        var population = new IMutableNeuralNetwork[settings.PopulationSize];
        for (int i = 0; i < population.Length; i++)
        {
            population[i] = DenseNetwork.Create(
                $"racer-gen0-{i}",
                RacingInputSize,
                hidden,
                RacingOutputSize,
                random: random);
        }

        var bestPerGen = new List<double>(settings.Generations);
        IMutableNeuralNetwork? bestEver = null;
        var bestFitnessEver = double.NegativeInfinity;

        for (int gen = 0; gen < settings.Generations; gen++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fitness = new double[population.Length];
            for (int i = 0; i < population.Length; i++)
            {
                fitness[i] = EvaluateEpisode(
                    track,
                    population[i],
                    rewardModel,
                    settings.MaxTicksPerEpisode,
                    cancellationToken,
                    holdoutTrack);
            }

            var ranked = fitness
                .Select((f, i) => (Index: i, Fitness: f))
                .OrderByDescending(x => x.Fitness)
                .ToArray();

            var genBestFitness = ranked[0].Fitness;
            bestPerGen.Add(genBestFitness);

            if (genBestFitness > bestFitnessEver)
            {
                bestFitnessEver = genBestFitness;
                bestEver = population[ranked[0].Index].Clone($"racer-champion-gen{gen}");
            }

            progress?.Report(new EvolutionaryRacingTrainerProgress(
                Generation: gen + 1,
                TotalGenerations: settings.Generations,
                GenerationBestFitness: genBestFitness,
                BestFitnessSoFar: bestFitnessEver));

            var rankedIndices = ranked.Select(r => r.Index).ToArray();
            settings.AfterGenerationEvaluated?.Invoke(new RacingGenerationEvaluated(
                Generation: gen + 1,
                TotalGenerations: settings.Generations,
                Track: track,
                Population: population,
                Fitness: fitness,
                RankedIndicesBestFirst: rankedIndices));

            var next = new IMutableNeuralNetwork[settings.PopulationSize];
            for (int e = 0; e < settings.EliteCount; e++)
                next[e] = population[ranked[e].Index].Clone(population[ranked[e].Index].Name + $"-elite{e}");

            for (int i = settings.EliteCount; i < settings.PopulationSize; i++)
            {
                var parentIdx = TournamentSelect(fitness, random, settings.TournamentSize);
                var child = population[parentIdx].Clone($"racer-gen{gen + 1}-{i}");
                child.Mutate(random, mutation);
                next[i] = child;
            }

            population = next;
        }

        if (bestEver is null)
            throw new InvalidOperationException("Training produced no champion.");

        return new EvolutionaryRacingTrainerResult(bestEver, bestFitnessEver, settings.Generations, bestPerGen);
    }

    private static int TournamentSelect(double[] fitness, Random random, int tournamentSize)
    {
        var bestIdx = random.Next(fitness.Length);
        var bestF = fitness[bestIdx];
        for (int t = 1; t < tournamentSize; t++)
        {
            var idx = random.Next(fitness.Length);
            if (fitness[idx] > bestF)
            {
                bestF = fitness[idx];
                bestIdx = idx;
            }
        }

        return bestIdx;
    }

    /// <summary>Runs one training episode and returns cumulative <see cref="IRewardModel"/> score (same signal as evolution fitness).</summary>
    public static double EvaluateEpisodeRewardSum(
        RaceTrack track,
        IMutableNeuralNetwork network,
        IRewardModel rewardModel,
        int maxTicks,
        CancellationToken cancellationToken = default)
    {
        var rewardSum = new double[1];
        var controller = new NeuralRaceCarController(network);
        var sim = new RaceSimulation(track, [controller], rewardModel, rewardSum);
        sim.Reset();

        for (int t = 0; t < maxTicks; t++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sim.Tick();
            if (sim.State.Cars[0].Crashed)
                break;
        }

        return rewardSum[0];
    }

    private static double EvaluateEpisode(
        RaceTrack trainTrack,
        IMutableNeuralNetwork network,
        IRewardModel rewardModel,
        int maxTicks,
        CancellationToken cancellationToken,
        RaceTrack? jointHoldoutTrack)
    {
        double train = EvaluateEpisodeRewardSum(trainTrack, network, rewardModel, maxTicks, cancellationToken);
        if (jointHoldoutTrack is null)
            return train;
        return train + EvaluateEpisodeRewardSum(jointHoldoutTrack, network, rewardModel, maxTicks, cancellationToken);
    }
}

public readonly record struct EvolutionaryRacingTrainerProgress(
    int Generation,
    int TotalGenerations,
    double GenerationBestFitness,
    double BestFitnessSoFar);
