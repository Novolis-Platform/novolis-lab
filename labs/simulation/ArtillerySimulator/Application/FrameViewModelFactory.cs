namespace ArtillerySimulator;

internal static class FrameViewModelFactory
{
    public static FrameViewModel Create(BattleWorld world) => new()
    {
        Camera = world.Camera,
        Terrain = world.Terrain,
        Atmosphere = world.Atmosphere,
        Gun = world.Gun,
        Crew = world.Crew,
        Rounds = world.Rounds,
        Aim = world.Aim,
        Battle = world.Battle,
        Particles = world.Particles,
        Mission = world.Mission,
        Level = world.Level,
        Ui = world.Ui,
        Audio = world.Audio,
        FireBlockReason = world.FireBlockReason(),
        SolutionUnsafe = world.SolutionUnsafe,
        LaunchDetonation = world.LaunchDetonation,
        FxTime = world.FxTime,
        FollowProjectile = world.Camera.FollowProjectile,
        PredictedRangeMeters = world.PredictedRangeMeters,
        Score = world.ScoreSnapshot,
        BatteryLabel = world.ActiveBatteryLabel,
    };
}
