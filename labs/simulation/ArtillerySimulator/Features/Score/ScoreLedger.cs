namespace ArtillerySimulator;

/// <summary>Accumulates arcade points for one mission run.</summary>
internal sealed class ScoreLedger
{
    private int _points;
    private int _shots;
    private int _kills;
    private int _pins;
    private int _roundsCleared;
    private int _lastSeenRound = 1;
    private bool _finalized;
    private MissionOutcome _outcome = MissionOutcome.Open;

    public int Points => _points;
    public int Shots => _shots;
    public int Kills => _kills;
    public int Pins => _pins;
    public int RoundsCleared => _roundsCleared;
    public bool Finalized => _finalized;
    public MissionOutcome Outcome => _outcome;

    public void Reset()
    {
        _points = 0;
        _shots = 0;
        _kills = 0;
        _pins = 0;
        _roundsCleared = 0;
        _lastSeenRound = 1;
        _finalized = false;
        _outcome = MissionOutcome.Open;
    }

    public void RegisterShot()
    {
        if (_finalized)
            return;
        _shots++;
    }

    public void RegisterImpact(int kills, int pins)
    {
        if (_finalized)
            return;
        if (kills > 0)
        {
            _kills += kills;
            _points += kills * ScoreRules.PointsPerKill;
        }

        if (pins > 0)
        {
            _pins += pins;
            _points += pins * ScoreRules.PointsPerPin;
        }
    }

    public void SyncRound(int missionRound)
    {
        if (_finalized || missionRound <= _lastSeenRound)
            return;

        // Round advanced after a clear — award for each step (usually +1).
        var gained = missionRound - _lastSeenRound;
        _roundsCleared += gained;
        _points += gained * ScoreRules.PointsPerRoundCleared;
        _lastSeenRound = missionRound;
    }

    public void Finalize(MissionOutcome outcome, int defendersAlive, float battleSeconds)
    {
        if (_finalized)
            return;

        _finalized = true;
        _outcome = outcome;
        switch (outcome)
        {
            case MissionOutcome.Held:
                _points += ScoreRules.HeldBonus;
                _points += Math.Max(0, defendersAlive) * ScoreRules.PointsPerDefenderAlive;
                var under = ScoreRules.HeldTimeTargetSeconds - battleSeconds;
                if (under > 0f)
                    _points += (int)(under * ScoreRules.HeldTimeBonusPerSecondUnder);
                break;
            case MissionOutcome.Fratricide:
                _points += ScoreRules.FratricidePenalty;
                break;
            case MissionOutcome.ObjectiveLost:
                _points += ScoreRules.ObjectiveLostPenalty;
                break;
        }

        if (_points < 0)
            _points = 0;
    }
}
