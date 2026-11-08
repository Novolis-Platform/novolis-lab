namespace ArtillerySimulator;

internal sealed class FireAuthorizationRules
{
    private readonly Func<GunModel> _gun;
    private readonly CrewDrill _crew;
    private readonly AimPreviewService _aim;
    private readonly Func<IDefenseMission?> _mission;

    public FireAuthorizationRules(
        Func<GunModel> gun,
        CrewDrill crew,
        AimPreviewService aim,
        Func<IDefenseMission?> mission)
    {
        _gun = gun;
        _crew = crew;
        _aim = aim;
        _mission = mission;
    }

    public float PredictedRangeMeters => _aim.PredictedRangeMeters();

    public bool LaunchDetonation => PredictedRangeMeters < _gun().Spec.LaunchClearanceMeters;

    public bool BelowHeMin => !LaunchDetonation && PredictedRangeMeters < _gun().Spec.MinEffectiveRangeMeters;

    public bool SolutionUnsafe => LaunchDetonation || BelowHeMin;

    public bool IsMissionTerminal => _mission()?.IsTerminal == true;

    public MissionOutcome MissionOutcome => _mission()?.Outcome ?? MissionOutcome.Open;

    public string? FireBlockReason()
    {
        if (IsMissionTerminal)
            return MissionOutcome switch
            {
                MissionOutcome.Held => "City held — mission complete.",
                MissionOutcome.Fratricide => "Fratricide — mission lost.",
                MissionOutcome.ObjectiveLost => "Crossing lost — mission over.",
                _ => "Mission complete.",
            };

        if (!_crew.BreechReady)
            return _crew.Phrase(_gun());
        if (LaunchDetonation)
            return "No clearance — predicted detonation at the piece.";
        return null;
    }
}
