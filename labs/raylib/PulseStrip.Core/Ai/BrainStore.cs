namespace PulseStrip.Core.Ai;

using Novolis.MachineLearning.Neural;
using Novolis.MachineLearning.Neural.Persistence;
using Novolis.Simulation.Racing.Tracks;

/// <summary>Loads or trains champion brains for race opponents.</summary>
public static class BrainStore
{
    public static IReadOnlyList<INeuralNetwork> LoadOrTrain(
        string brainsDirectory,
        int count,
        ITrackDefinition? trainTrack = null,
        bool forceRetrain = false,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(brainsDirectory);
        var serializer = new JsonNeuralNetworkSerializer();
        var networks = new List<INeuralNetwork>(count);

        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(brainsDirectory, $"champion-{i}.json");
            if (!forceRetrain && File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var snapshot = serializer.Deserialize(json);
                networks.Add(DenseNetwork.FromSnapshot(snapshot));
                continue;
            }

            var settings = new EvolutionaryHoverTrainerSettings(
                Track: trainTrack ?? BuiltInTracks.MicroCircle,
                PopulationSize: 12,
                Generations: 6,
                MaxTicksPerEpisode: 700,
                RandomSeed: 42 + i * 17);

            var result = new EvolutionaryHoverTrainer().Train(settings, cancellationToken);
            var champion = result.Champion;
            if (champion is DenseNetwork dense)
            {
                var snapshot = dense.ToSnapshot($"champion-{i}");
                File.WriteAllText(path, serializer.Serialize(snapshot));
                networks.Add(dense);
            }
            else
            {
                networks.Add(champion);
            }
        }

        return networks;
    }
}
