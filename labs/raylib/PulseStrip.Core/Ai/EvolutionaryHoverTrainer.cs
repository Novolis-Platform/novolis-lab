namespace PulseStrip.Core.Ai;

using Novolis.MachineLearning.Neural;
using Novolis.Simulation.Racing.Rewards;
using Novolis.Simulation.Racing.Tracks;

/// <summary>Evolves <see cref="DenseNetwork"/> hover agents on PulseStrip circuits.</summary>
public sealed class EvolutionaryHoverTrainer
{
    private readonly ITrackBuilder _trackBuilder;

    public EvolutionaryHoverTrainer(ITrackBuilder? trackBuilder = null)
    {
        _trackBuilder = trackBuilder ?? new TrackBuilder();
    }

    public EvolutionaryHoverTrainerResult Train(
        EvolutionaryHoverTrainerSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.PopulationSize, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.Generations, 1);

        var random = settings.RandomSeed == 0 ? new Random() : new Random(settings.RandomSeed);
        var track = _trackBuilder.Build(settings.Track);
        var rewardModel = new DefaultRewardModel();
        var mutation = settings.MutationOrDefault;
        var hidden = settings.HiddenLayerSizesOrDefault;

        var population = new IMutableNeuralNetwork[settings.PopulationSize];
        for (var i = 0; i < population.Length; i++)
        {
            population[i] = DenseNetwork.Create(
                $"hover-gen0-{i}",
                HoverRaceSimulation.SensorInputSize,
                hidden,
                HoverRaceSimulation.ControlOutputSize,
                random: random);
        }

        var bestPerGen = new List<double>(settings.Generations);
        IMutableNeuralNetwork? bestEver = null;
        var bestFitnessEver = double.NegativeInfinity;

        for (var gen = 0; gen < settings.Generations; gen++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fitness = new double[population.Length];
            for (var i = 0; i < population.Length; i++)
            {
                fitness[i] = EvaluateEpisode(
                    track,
                    population[i],
                    rewardModel,
                    settings.MaxTicksPerEpisode,
                    cancellationToken);
            }

            var ranked = fitness
                .Select((f, i) => (Index: i, Fitness: f))
                .OrderByDescending(x => x.Fitness)
                .ToArray();

            var genBest = ranked[0].Fitness;
            bestPerGen.Add(genBest);
            if (genBest > bestFitnessEver)
            {
                bestFitnessEver = genBest;
                bestEver = population[ranked[0].Index].Clone($"hover-champion-gen{gen}");
            }

            var next = new IMutableNeuralNetwork[settings.PopulationSize];
            for (var e = 0; e < settings.EliteCount; e++)
                next[e] = population[ranked[e].Index].Clone(population[ranked[e].Index].Name + $"-elite{e}");

            for (var i = settings.EliteCount; i < settings.PopulationSize; i++)
            {
                var parentIdx = TournamentSelect(fitness, random, settings.TournamentSize);
                var child = population[parentIdx].Clone($"hover-gen{gen + 1}-{i}");
                child.Mutate(random, mutation);
                next[i] = child;
            }

            population = next;
        }

        if (bestEver is null)
            throw new InvalidOperationException("Training produced no champion.");

        return new EvolutionaryHoverTrainerResult(bestEver, bestFitnessEver, settings.Generations, bestPerGen);
    }

    public static double EvaluateEpisode(
        RaceTrack track,
        IMutableNeuralNetwork network,
        IRewardModel rewardModel,
        int maxTicks,
        CancellationToken cancellationToken = default)
    {
        var accum = new double[1];
        var controller = new NeuralHoverController(network);
        var sim = new HoverRaceSimulation(track, [controller], targetLaps: 2, rewardModel, accum);
        sim.Reset();

        for (var t = 0; t < maxTicks; t++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sim.Tick();
            if (sim.State.Craft[0].Crashed || sim.State.Craft[0].Finished)
                break;
        }

        return accum[0];
    }

    private static int TournamentSelect(double[] fitness, Random random, int tournamentSize)
    {
        var bestIdx = random.Next(fitness.Length);
        var bestF = fitness[bestIdx];
        for (var t = 1; t < tournamentSize; t++)
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
}

public readonly record struct EvolutionaryHoverTrainerResult(
    IMutableNeuralNetwork Champion,
    double BestFitness,
    int Generations,
    IReadOnlyList<double> BestFitnessPerGeneration);
