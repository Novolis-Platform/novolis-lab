using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>Recycles ballistic runners so multi-round fire stays bounded.</summary>
internal sealed class ArtilleryRoundPool
{
    public const int MaxLiveRounds = 18;

    private readonly List<BallisticTrajectoryRunner> _rounds = [];
    private readonly Func<BallisticTrajectoryRunner> _factory;
    private BallisticTrajectoryRunner _active;

    public ArtilleryRoundPool(Func<BallisticTrajectoryRunner> factory)
    {
        _factory = factory;
        _active = factory();
        _rounds.Add(_active);
    }

    public BallisticTrajectoryRunner Active
    {
        get => _active;
        set => _active = value;
    }

    public IReadOnlyList<BallisticTrajectoryRunner> Rounds => _rounds;

    public BallisticTrajectoryRunner Acquire(
        bool followProjectile,
        HashSet<BallisticTrajectoryRunner> impactHandled)
    {
        foreach (var round in _rounds)
        {
            if (round.Phase is BallisticTrajectoryPhase.Ready)
                return round;
            if (round.Phase == BallisticTrajectoryPhase.Impacted
                && round != _active
                && !followProjectile)
            {
                impactHandled.Remove(round);
                round.Reset();
                return round;
            }
        }

        if (_rounds.Count >= MaxLiveRounds)
        {
            for (var i = 0; i < _rounds.Count; i++)
            {
                if (_rounds[i].Phase != BallisticTrajectoryPhase.InFlight && _rounds[i] != _active)
                {
                    impactHandled.Remove(_rounds[i]);
                    _rounds[i].Reset();
                    return _rounds[i];
                }
            }

            var steal = _rounds[0];
            impactHandled.Remove(steal);
            steal.Reset();
            return steal;
        }

        var created = _factory();
        _rounds.Add(created);
        return created;
    }

    public void ResetAll(HashSet<BallisticTrajectoryRunner> impactHandled)
    {
        foreach (var round in _rounds)
            round.Reset();
        impactHandled.Clear();
        _active = _rounds[0];
    }

    public BallisticTrajectoryRunner? RoundForCamera(float burstAge, float killcamReturnSeconds)
    {
        for (var i = _rounds.Count - 1; i >= 0; i--)
        {
            if (_rounds[i].Phase == BallisticTrajectoryPhase.InFlight)
                return _rounds[i];
        }

        if (burstAge >= 0f && burstAge < killcamReturnSeconds)
        {
            for (var i = _rounds.Count - 1; i >= 0; i--)
            {
                if (_rounds[i].Phase == BallisticTrajectoryPhase.Impacted)
                    return _rounds[i];
            }
        }

        return null;
    }
}
