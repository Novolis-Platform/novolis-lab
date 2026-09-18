namespace PulseStrip.Core.Ai;

using Novolis.MachineLearning.Neural;
using Novolis.Simulation.Racing.Tracks;

/// <summary>Settings for evolving hover-race neural opponents.</summary>
public sealed record EvolutionaryHoverTrainerSettings(
    ITrackDefinition Track,
    int PopulationSize = 16,
    int Generations = 8,
    int MaxTicksPerEpisode = 900,
    int[]? HiddenLayerSizes = null,
    MutationSettings? Mutation = null,
    int TournamentSize = 3,
    int EliteCount = 2,
    int RandomSeed = 42)
{
    public int[] HiddenLayerSizesOrDefault => HiddenLayerSizes ?? [16, 12];

    public MutationSettings MutationOrDefault => Mutation ?? new MutationSettings(
        WeightMutationRate: 0.14,
        WeightMutationSigma: 0.30,
        BiasMutationRate: 0.12,
        BiasMutationSigma: 0.10);
}
