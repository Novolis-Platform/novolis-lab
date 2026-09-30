using Novolis.Geopolitics.Core;
using Novolis.Geopolitics.Scenarios;
using Novolis.Geopolitics.Simulation;

namespace GeoPolity.Session;

/// <summary>Shared session: world + composition runner + clock + headlines. Shells only observe/enqueue.</summary>
public sealed class GeoSession
{
    public GeoSession(WorldState world, int? rngSeed = null)
    {
        World = world;
        Simulation = new WorldSimulation(world, rngSeed);
        Clock = new SessionClockController();
        Headlines = new HeadlineFeedController();
        OpeningOwnership = world.Provinces.ToDictionary(p => p.Id.Value, p => p.OwnerId.Value);
        PlayerSystemId = world.Polities.OrderByDescending(p => p.Gdp).First().Id;
        SelectedSystemId = PlayerSystemId;
    }

    public static GeoSession LoadDefault(int? rngSeed = null) =>
        new(DefaultWorld.Load(), rngSeed);

    public WorldState World { get; }
    public WorldSimulation Simulation { get; }
    public SessionClockController Clock { get; }
    public HeadlineFeedController Headlines { get; }
    public IReadOnlyDictionary<int, int> OpeningOwnership { get; }
    public PolityId PlayerSystemId { get; }
    public PolityId SelectedSystemId { get; private set; }
    public bool QuitRequested { get; private set; }

    public Polity Player => World.Polity(PlayerSystemId);
    public Polity Selected => World.Polity(SelectedSystemId);

    public void RequestQuit() => QuitRequested = true;

    public void SelectSystem(PolityId id)
    {
        if (id.Value < 0 || id.Value >= World.Polities.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        SelectedSystemId = id;
        Clock.StatusNote = $"focus {World.Polity(id).Name}";
    }

    /// <summary>Advance one session pulse when clock allows.</summary>
    public void PulseIfRunning()
    {
        if (!Clock.ShouldPulse)
        {
            return;
        }

        AdvanceDays(Clock.DaysPerPulse);
    }

    public void AdvanceDays(int days)
    {
        if (days <= 0)
        {
            return;
        }

        Simulation.Advance(days);
        Headlines.SyncFrom(World);
    }

    public void AdvanceYears(int years)
    {
        var y = Math.Max(1, years);
        AdvanceDays(y * WorldState.DaysPerYear);
    }

    public int OwnershipChurn() =>
        World.Provinces.Count(p => OpeningOwnership[p.Id.Value] != p.OwnerId.Value);
}
