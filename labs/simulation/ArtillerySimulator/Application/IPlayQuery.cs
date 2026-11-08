using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>Read port for agent snapshots / HUD — no Silk, no command side effects.</summary>
internal interface IPlayQuery
{
    IGameLevel Level { get; }
    bool IsPaused { get; }
    bool WorldReady { get; }
    GunModel Gun { get; }
    CrewDrill Crew { get; }
    BallisticTrajectoryRunner Shot { get; }
    TerrainWorld Terrain { get; }
    AtmosphereModel Atmosphere { get; }
    IDefenseMission? Mission { get; }
    BattleSession Session { get; }
    DeliverySampler Delivery { get; }
    float PredictedRangeMeters { get; }
    string CameraModeName { get; }
    string PaceText { get; }
    string? FireBlockReason();
    bool SolutionUnsafe { get; }
    bool LaunchDetonation { get; }
}
