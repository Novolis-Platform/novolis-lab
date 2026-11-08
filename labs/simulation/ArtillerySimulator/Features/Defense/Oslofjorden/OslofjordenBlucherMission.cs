using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// Educational 9 April 1940 vignette: Oscarsborg coastal guns vs an invading German fleet
/// steaming the Oslofjorden fairways. Decorative coastal roads are scenery; ships use
/// invisible naval paths.
/// </summary>
internal sealed class OslofjordenBlucherMission : IDefenseMission
{
    private readonly DefenseBattlefieldState _state;

    public OslofjordenBlucherMission(TerrainWorld terrain, IArtilleryPieceSpec piece, int seed)
    {
        _state = new DefenseBattlefieldState(terrain, piece, seed)
        {
            SouthernApproachesOnly = true,
        };
        var boot = OslofjordenMissionBootFactory.From(terrain, piece);
        _state.Objective = boot.Objective;
        _state.GunX = boot.GunX;
        _state.GunZ = boot.GunZ;
        _state.TrailBearingDegrees = boot.TrailBearingDegrees;

        PlaceIslandGarrison();
        var sw = Stopwatch.StartNew();
        NavalWaveSpawner.SeedFairways(_state);
        var afterSpawns = sw.ElapsedMilliseconds;
        NavalWaveSpawner.ApplyRoundDifficulty(_state);
        _state.NextWaveAt = 0f;
        NavalWaveSpawner.SpawnWave(_state);
        _state.NextWaveAt = _state.BattleSeconds + NavalWaveSpawner.WaveIntervalSeconds(_state);

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
            $"Oslofjorden boot: seed={seed:x8} piece={piece.Id} fairways={afterSpawns}ms tune={sw.ElapsedMilliseconds - afterSpawns}ms total={sw.ElapsedMilliseconds}ms lanes={_state.SpawnRoutes.Count}"));
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
        _state.Approaches.Count == 0 ? "the fairways" : string.Join(", ", _state.Approaches);
    public int Remaining => _state.Units.Count;
    public int Stopped => _state.Wrecks.Count;
    public int DefendersAlive => _state.DefendersAlive;
    public float BattleElapsedSeconds => _state.BattleSeconds;
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
                NavalWaveSpawner.ApplyRoundDifficulty(_state);
            }

            NavalWaveSpawner.SpawnWave(_state);
            _state.NextWaveAt = _state.BattleSeconds + NavalWaveSpawner.WaveIntervalSeconds(_state);
        }

        ColumnConvoySimulator.AdvanceUnits(_state, simSeconds);
    }

    public void ResolveImpact(Vector3 point) => WarheadEffectsResolver.ResolveImpact(_state, point);

    public void RetuneEquipment(IArtilleryPieceSpec piece)
    {
        ArgumentNullException.ThrowIfNull(piece);
        _state.Piece = piece;
        _state.CaughtMeters = piece.Warhead.CaughtMeters;
        _state.PinnedMeters = piece.Warhead.PinnedMeters;
    }

    public void Draw(SilkWireBatch wires) =>
        NavalSceneDraw.Draw(
            wires,
            _state.Terrain,
            _state.Objective,
            DefenseBattlefieldState.ObjectiveRadiusMeters,
            _state.Units,
            _state.Wrecks,
            _state.Lanes,
            _state.Defenders);

    public string HudLine() => DefenseVictoryEvaluator.Outcome(_state) switch
    {
        MissionOutcome.Fratricide => "FRATRICIDE — fortress silenced",
        MissionOutcome.ObjectiveLost => "FJORD LOST — fleet broke north",
        MissionOutcome.Held => $"OSCARSBORG HELD    {DefendersAlive} guns    {Stopped} sunk",
        _ => $"9 APR  ROUND {_state.Round}  GROUP {_state.WaveSerial}  {Remaining} AFLOAT  NEAREST {NearestMeters / 1000f:F1} KM",
    };

    public string Observation()
    {
        if (_state.FratricideLoss)
            return "Friendly battery hit. Oscarsborg is silenced.";
        if (_state.ObjectiveLost)
            return "The invasion fleet forced the narrows toward Oslo.";
        if (DefenseVictoryEvaluator.Held(_state))
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Oscarsborg held the fjord. {Stopped} ships stopped, {DefendersAlive} guns still manned.");

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Night of 9 April 1940. Hold Oscarsborg. Round {_state.Round}, {Remaining} ships on {AvenueName}, nearest {NearestMeters / 1000f:F2} km.");
    }

    public string ResultPhrase()
    {
        if (_state.ObjectiveLost)
            return "The fjord is lost.";
        if (DefenseVictoryEvaluator.Held(_state))
            return string.Create(
                CultureInfo.InvariantCulture,
                $"The narrows held. {Stopped} ships stopped — Blücher's lesson remembered.");

        return string.Create(
            CultureInfo.InvariantCulture,
            $"The salvo put {LastBracket} down and pinned {LastPinned}. {Remaining} still steaming, nearest {NearestMeters / 1000f:F2} km.");
    }

    public string PostureToken() => DefenseMissionNarration.PostureToken(_state);
    public Vector3 NearestAlive(Vector3 gun) => ThreatGeometryQueries.NearestAlive(_state, gun);
    public Vector3 BattleFocus(Vector3 gun) => ThreatGeometryQueries.BattleFocus(_state, gun);
    public ThreatReport[] Reports(Vector3 gun) => ThreatReportBuilder.Reports(_state, gun);
    public DefenderReport[] DefenderReports() => ThreatReportBuilder.DefenderReports(_state);

    private void PlaceIslandGarrison()
    {
        AddGun("O1", Bearing(40f, 90f));
        AddGun("O2", Bearing(140f, 110f));
        AddGun("O3", Bearing(250f, 95f));
        AddGun("O4", Bearing(320f, 85f));
    }

    private Vector2 Bearing(float bearingDegrees, float meters)
    {
        var rad = bearingDegrees * (MathF.PI / 180f);
        var seed = new Vector2(
            _state.GunX + MathF.Cos(rad) * meters,
            _state.GunZ + MathF.Sin(rad) * meters);
        if (_state.Terrain.Profile.IsWater(seed.X, seed.Y))
            return new Vector2(_state.GunX, _state.GunZ);
        return seed;
    }

    private void AddGun(string label, Vector2 at)
    {
        _state.Defenders.Add(new DefenderUnit
        {
            Label = label,
            X = at.X,
            Z = at.Y,
            YawDegrees = OpeningFirePlan.BearingDegrees(
                new Vector3(_state.GunX, 0f, _state.GunZ),
                new Vector3(at.X, 0f, at.Y)),
            Alive = true,
        });
    }
}
