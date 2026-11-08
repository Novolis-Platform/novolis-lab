using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// Customer brief — Defense of Kristiansand: OPFOR advances on the city; garrison at
/// Kvadraturen cannot hold alone; one K9 DOG sandbox piece on Odderøya must break the columns.
/// </summary>
internal sealed class KristiansandDefenseMission : IDefenseMission
{
    private readonly DefenseBattlefieldState _state;

    public KristiansandDefenseMission(TerrainWorld terrain, IArtilleryPieceSpec piece, int seed)
    {
        _state = new DefenseBattlefieldState(terrain, piece, seed);
        var boot = KristiansandMissionBoot.From(terrain, piece);
        ApplyBoot(boot);

        DowntownDefenderPlacement.PlaceDefenders(_state);
        var sw = Stopwatch.StartNew();
        OpforSpawnCatalog.BuildSpawnCatalog(_state);
        var afterSpawns = sw.ElapsedMilliseconds;
        OpforDifficultyCurve.ApplyRoundDifficulty(_state);
        _state.NextWaveAt = 0f;
        ColumnWaveSpawner.SpawnWave(_state);
        _state.NextWaveAt = _state.BattleSeconds + OpforDifficultyCurve.WaveIntervalSeconds(_state);

        ThreatGeometryQueries.RefreshNearest(_state);
        var lay = OpeningFirePlan.Tune(
            terrain,
            boot.Site.Position,
            _state.TrailBearingDegrees,
            boot.Site.OpeningElevationDegrees,
            _state.Units,
            _state.Objective,
            piece);
        _state.OpeningElevationDegrees = lay.Elevation;
        _state.OpeningChargeIndex = lay.Charge;
        _state.OpeningAzimuthDegrees = lay.Bearing;
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Mission boot: seed={seed:x8} piece={piece.Id} spawns={afterSpawns}ms tune={sw.ElapsedMilliseconds - afterSpawns}ms total={sw.ElapsedMilliseconds}ms approaches={_state.SpawnRoutes.Count}"));
    }

    private void ApplyBoot(KristiansandMissionBoot boot)
    {
        _state.Objective = boot.Objective;
        _state.GunX = boot.GunX;
        _state.GunZ = boot.GunZ;
        _state.TrailBearingDegrees = boot.TrailBearingDegrees;
    }

    public float CaughtMeters => _state.CaughtMeters;
    public float PinnedMeters => _state.PinnedMeters;
    public float GunX => _state.GunX;
    public float GunZ => _state.GunZ;
    public Vector3 Objective => _state.Objective;
    public float TrailBearingDegrees => _state.TrailBearingDegrees;
    public float OpeningElevationDegrees => _state.OpeningElevationDegrees;
    public int OpeningChargeIndex => _state.OpeningChargeIndex;
    public float OpeningAzimuthDegrees => _state.OpeningAzimuthDegrees;
    public bool ObjectiveLost => _state.ObjectiveLost;
    public bool FratricideLoss => _state.FratricideLoss;
    public int LastBracket => _state.LastBracket;
    public int LastPinned => _state.LastPinned;
    public float NearestMeters => _state.NearestMeters;
    public int Round => _state.Round;
    public int SpawnPointCount => _state.SpawnRoutes.Count;
    public string AvenueName =>
        _state.Approaches.Count == 0 ? "the primary roads" : string.Join(", ", _state.Approaches);
    public int Remaining => _state.Units.Count;
    public int Stopped => _state.Wrecks.Count;
    public int DefendersAlive => _state.DefendersAlive;
    public bool Held => DefenseVictoryEvaluator.Held(_state);
    public MissionOutcome Outcome => DefenseVictoryEvaluator.Outcome(_state);

    public void Advance(float simSeconds, Vector3? incomingShell)
    {
        if (Outcome is not MissionOutcome.Open || simSeconds <= 0f)
            return;

        if (DefenseVictoryEvaluator.EvaluateHeld(_state))
        {
            _state.HeldLatched = true;
            return;
        }

        _state.BattleSeconds += simSeconds;

        if (_state.BattleSeconds >= _state.NextWaveAt)
        {
            if (Remaining == 0 && _state.WaveSerial > 0)
            {
                _state.Round++;
                OpforDifficultyCurve.ApplyRoundDifficulty(_state);
            }

            ColumnWaveSpawner.SpawnWave(_state);
            _state.NextWaveAt = _state.BattleSeconds + OpforDifficultyCurve.WaveIntervalSeconds(_state);
        }

        ColumnConvoySimulator.AdvanceUnits(_state, simSeconds);
    }

    public void ResolveImpact(Vector3 point) => WarheadEffectsResolver.ResolveImpact(_state, point);

    public void Draw(SilkWireBatch wires) =>
        DefenseSceneDraw.Draw(
            wires,
            _state.Terrain,
            _state.Objective,
            DefenseBattlefieldState.ObjectiveRadiusMeters,
            _state.Units,
            _state.Wrecks,
            _state.Lanes,
            _state.Defenders);

    public string HudLine() => DefenseMissionNarration.HudLine(_state);
    public string Observation() => DefenseMissionNarration.Observation(_state);
    public string ResultPhrase() => DefenseMissionNarration.ResultPhrase(_state);
    public string PostureToken() => DefenseMissionNarration.PostureToken(_state);
    public Vector3 NearestAlive(Vector3 gun) => ThreatGeometryQueries.NearestAlive(_state, gun);
    public Vector3 BattleFocus(Vector3 gun) => ThreatGeometryQueries.BattleFocus(_state, gun);
    public ThreatReport[] Reports(Vector3 gun) => ThreatReportBuilder.Reports(_state, gun);
    public DefenderReport[] DefenderReports() => ThreatReportBuilder.DefenderReports(_state);
}
