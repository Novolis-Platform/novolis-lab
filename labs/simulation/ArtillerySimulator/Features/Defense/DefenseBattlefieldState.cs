using System.Numerics;

namespace ArtillerySimulator;

/// <summary>Mutable battlefield lists and mission flags shared by defense collaborators.</summary>
internal sealed class DefenseBattlefieldState
{
    public const float ObjectiveRadiusMeters = 180f;
    public const float BaseColumnSpeedMetersPerSecond = 12.5f;
    public const float SpacingMeters = 55f;
    public const int MaxTrackedWrecks = 96;

    public TerrainWorld Terrain { get; }
    public IArtilleryPieceSpec Piece { get; }
    public Random Rng { get; }

    public List<string> Approaches { get; } = [];
    public List<RouteLane> Lanes { get; } = [];
    public List<ColumnUnit> Units { get; } = [];
    public List<ColumnUnit> Wrecks { get; } = [];
    public List<DefenderUnit> Defenders { get; } = [];
    public List<GroundPolyline> SpawnRoutes { get; } = [];
    public List<int> SpawnDeck { get; } = [];

    public float BattleSeconds;
    public int Round = 1;
    public float NextWaveAt;
    public float ColumnSpeed = BaseColumnSpeedMetersPerSecond;
    public int WaveSerial;
    public bool HeldLatched;

    public bool ObjectiveLost;
    public bool FratricideLoss;
    public int LastBracket;
    public int LastPinned;
    public float NearestMeters;

    public float CaughtMeters { get; init; }
    public float PinnedMeters { get; init; }
    public float GunX { get; set; }
    public float GunZ { get; set; }
    public Vector3 Objective { get; set; }
    public float TrailBearingDegrees { get; set; }
    public float OpeningElevationDegrees { get; set; }
    public int OpeningChargeIndex { get; set; }
    public float OpeningAzimuthDegrees { get; set; }

    public DefenseBattlefieldState(TerrainWorld terrain, IArtilleryPieceSpec piece, int seed)
    {
        Terrain = terrain;
        Piece = piece ?? throw new ArgumentNullException(nameof(piece));
        Rng = new Random(seed ^ unchecked((int)0x4B3951u));
        CaughtMeters = piece.Warhead.CaughtMeters;
        PinnedMeters = piece.Warhead.PinnedMeters;
    }

    public int DefendersAlive => Defenders.Count(d => d.Alive);

    public float Height(float x, float z) => Terrain.Field.SampleHeight(x, z);
}
