namespace NeuralRacing.Training;

using Novolis.Simulation.Racing.Rewards;
using Novolis.Simulation.Racing.Tracks;
using Novolis.MachineLearning.Neural;

public sealed record EvolutionaryRacingTrainerSettings(
    ITrackDefinition Track,
    int PopulationSize = 24,
    int Generations = 40,
    int MaxTicksPerEpisode = 3600,
    int[]? HiddenLayerSizes = null,
    MutationSettings? Mutation = null,
    IRewardModel? RewardModel = null,
    int TournamentSize = 3,
    int EliteCount = 2,
    int RandomSeed = 0,
    Action<RacingGenerationEvaluated>? AfterGenerationEvaluated = null,
    /// <summary>When <see cref="JointHoldoutFitness"/> is true, each genome is scored as train-episode reward plus a full episode on this track (generalization pressure).</summary>
    ITrackDefinition? HoldoutTrack = null,
    bool JointHoldoutFitness = false)
{
    public int[] HiddenLayerSizesOrDefault => HiddenLayerSizes ?? [16, 16];

    public MutationSettings MutationOrDefault => Mutation ?? new MutationSettings(
        WeightMutationRate: 0.12,
        WeightMutationSigma: 0.28,
        BiasMutationRate: 0.12,
        BiasMutationSigma: 0.08);

    public IRewardModel RewardModelOrDefault => RewardModel ?? new DefaultRewardModel();

    /// <summary>Validates joint holdout configuration before training.</summary>
    public void Validate()
    {
        if (JointHoldoutFitness && HoldoutTrack is null)
            throw new ArgumentException("JointHoldoutFitness requires HoldoutTrack.", nameof(HoldoutTrack));
        if (!JointHoldoutFitness && HoldoutTrack is not null)
            throw new ArgumentException("HoldoutTrack is only used when JointHoldoutFitness is true.", nameof(HoldoutTrack));
    }
}
