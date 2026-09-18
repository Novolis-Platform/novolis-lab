namespace NeuralRacing.Tests;

using NeuralRacing.Training;
using Novolis.Simulation.Racing.Tracks;

using TUnit.Assertions;

public sealed class EvolutionaryRacingTrainerValidationTests
{
    private sealed class SyncProgressList : IProgress<EvolutionaryRacingTrainerProgress>
    {
        private readonly List<EvolutionaryRacingTrainerProgress> _list;
        public SyncProgressList(List<EvolutionaryRacingTrainerProgress> list) => _list = list;
        public void Report(EvolutionaryRacingTrainerProgress value) => _list.Add(value);
    }

    private static EvolutionaryRacingTrainerSettings Base() =>
        new(
            Track: BuiltInTracks.Circle,
            PopulationSize: 4,
            Generations: 2,
            MaxTicksPerEpisode: 200,
            EliteCount: 1,
            TournamentSize: 2,
            RandomSeed: 1);

    [Test]
    public async Task Train_PopulationSizeLessThanTwo_Throws()
    {
        var s = Base() with { PopulationSize = 1 };
        await Assert.That(() => new EvolutionaryRacingTrainer().Train(s))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Train_GenerationsLessThanOne_Throws()
    {
        var s = Base() with { Generations = 0 };
        await Assert.That(() => new EvolutionaryRacingTrainer().Train(s))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Train_MaxTicksLessThanOne_Throws()
    {
        var s = Base() with { MaxTicksPerEpisode = 0 };
        await Assert.That(() => new EvolutionaryRacingTrainer().Train(s))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Train_TournamentSizeLessThanTwo_Throws()
    {
        var s = Base() with { TournamentSize = 1 };
        await Assert.That(() => new EvolutionaryRacingTrainer().Train(s))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Train_EliteCountLessThanOne_Throws()
    {
        var s = Base() with { EliteCount = 0 };
        await Assert.That(() => new EvolutionaryRacingTrainer().Train(s))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Train_EliteCountEqualsPopulation_ThrowsArgumentException()
    {
        var s = Base() with { PopulationSize = 4, EliteCount = 4 };
        await Assert.That(() => new EvolutionaryRacingTrainer().Train(s))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Train_CancelledToken_ThrowsOperationCanceledException()
    {
        var s = Base() with { Generations = 50, MaxTicksPerEpisode = 4000 };
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.That(() => new EvolutionaryRacingTrainer().Train(s, cancellationToken: cts.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Train_Progress_ReportsOncePerGeneration()
    {
        var reports = new List<EvolutionaryRacingTrainerProgress>();
        var s = Base() with { Generations = 3 };
        new EvolutionaryRacingTrainer().Train(s, new SyncProgressList(reports));
        await Assert.That(reports.Count).IsEqualTo(3);
        await Assert.That(reports.Select(r => r.Generation).ToArray()).IsEquivalentTo(new[] { 1, 2, 3 });
        await Assert.That(reports[^1].TotalGenerations).IsEqualTo(3);
    }

    [Test]
    [MethodDataSource(nameof(ReasonableTracks))]
    public async Task Train_OneGenerationPerReasonableTrack_Completes(ITrackDefinition track)
    {
        var settings = new EvolutionaryRacingTrainerSettings(
            Track: track,
            PopulationSize: 4,
            Generations: 1,
            MaxTicksPerEpisode: 400,
            EliteCount: 1,
            TournamentSize: 2,
            RandomSeed: 42);

        var result = new EvolutionaryRacingTrainer().Train(settings);
        await Assert.That(result.GenerationsRun).IsEqualTo(1);
        await Assert.That(result.BestNetwork.InputSize).IsEqualTo(EvolutionaryRacingTrainer.RacingInputSize);
        await Assert.That(result.BestNetwork.OutputSize).IsEqualTo(EvolutionaryRacingTrainer.RacingOutputSize);
    }

    public static IEnumerable<Func<ITrackDefinition>> ReasonableTracks() =>
        BuiltInTracks.Reasonable.Select<ITrackDefinition, Func<ITrackDefinition>>(t => () => t);

    [Test]
    public async Task Train_SettingsValidate_JointWithoutHoldout_Throws()
    {
        var s = Base() with { JointHoldoutFitness = true, HoldoutTrack = null };
        await Assert.That(() => new EvolutionaryRacingTrainer().Train(s))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Train_SettingsValidate_HoldoutWithoutJoint_Throws()
    {
        var s = Base() with { JointHoldoutFitness = false, HoldoutTrack = BuiltInTracks.MicroCircle };
        await Assert.That(() => new EvolutionaryRacingTrainer().Train(s))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Train_JointHoldoutFitness_SmallRun_Completes()
    {
        var settings = new EvolutionaryRacingTrainerSettings(
            Track: BuiltInTracks.MicroCircle,
            HoldoutTrack: BuiltInTracks.CompactOval,
            JointHoldoutFitness: true,
            PopulationSize: 4,
            Generations: 2,
            MaxTicksPerEpisode: 300,
            EliteCount: 1,
            TournamentSize: 2,
            RandomSeed: 99);

        var result = new EvolutionaryRacingTrainer().Train(settings);
        await Assert.That(result.GenerationsRun).IsEqualTo(2);
        await Assert.That(result.BestFitness).IsNotEqualTo(double.NegativeInfinity);
    }
}
