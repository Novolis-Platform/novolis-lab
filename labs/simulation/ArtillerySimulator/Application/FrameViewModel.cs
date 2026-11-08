namespace ArtillerySimulator;

/// <summary>
/// One-frame render bundle. Presentation reads this instead of reaching into
/// <see cref="BattleWorld"/> through a 15-parameter draw method.
/// </summary>
internal sealed class FrameViewModel
{
    public required ArtilleryCameras Camera { get; init; }
    public required TerrainWorld Terrain { get; init; }
    public required AtmosphereModel Atmosphere { get; init; }
    public required GunModel Gun { get; init; }
    public required CrewDrill Crew { get; init; }
    public required ArtilleryRoundPool Rounds { get; init; }
    public required AimPreviewService Aim { get; init; }
    public required ArtilleryBattleLoop Battle { get; init; }
    public required ArtilleryParticles Particles { get; init; }
    public required IDefenseMission? Mission { get; init; }
    public required IGameLevel Level { get; init; }
    public required FireControlUi Ui { get; init; }
    public required ArtilleryAudioHub? Audio { get; init; }
    public required string? FireBlockReason { get; init; }
    public required bool SolutionUnsafe { get; init; }
    public required bool LaunchDetonation { get; init; }
    public required float FxTime { get; init; }
    public required bool FollowProjectile { get; init; }
    public required float PredictedRangeMeters { get; init; }
    public required ScoreSnapshot Score { get; init; }
}
