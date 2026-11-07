using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// Customer brief — Defense of Kristiansand: OPFOR advances on the city; garrison at
/// Kvadraturen cannot hold alone; one K9 DOG sandbox piece on Odderøya must break the columns.
/// Downtown (~1.2 km) is inside HE min range; the battery engages approaches on the primary-road
/// spawn ring (1.25× HE min). Burst distances are a game rule, not a weapon table.
/// </summary>
internal sealed class KristiansandDefenseMission : IDefenseMission
{
    public const float ObjectiveRadiusMeters = 180f;
    /// <summary>
    /// Wheeled column on primary roads (~45 km/h). Soft margin keeps spawn/place inside the cutout.
    /// </summary>
    public const float BaseColumnSpeedMetersPerSecond = 12.5f;
    /// <summary>Convoy gap along the road centerline (bumper-ish spacing).</summary>
    public const float SpacingMeters = 55f;
    /// <summary>Tiny inset so units/roads are not glued to the far clip edge.</summary>
    private const float MapMarginMeters = 20f;
    private const int MaxTrackedWrecks = 96;

    private readonly TerrainWorld _terrain;
    private readonly IArtilleryPieceSpec _piece;
    private readonly List<string> _approaches = [];
    private readonly List<RouteLane> _lanes = [];
    private readonly List<ColumnUnit> _units = [];
    private readonly List<ColumnUnit> _wrecks = [];
    private readonly List<DefenderUnit> _defenders = [];
    private readonly List<GroundPolyline> _spawnRoutes = [];
    private readonly Random _rng;
    private readonly List<int> _spawnDeck = [];
    private float _battleSeconds;
    private int _round = 1;
    private float _nextWaveAt;
    private float _columnSpeed = BaseColumnSpeedMetersPerSecond;
    private int _waveSerial;
    private bool _heldLatched;

    public KristiansandDefenseMission(TerrainWorld terrain, IArtilleryPieceSpec piece, int seed)
    {
        _terrain = terrain;
        _piece = piece ?? throw new ArgumentNullException(nameof(piece));
        _rng = new Random(seed ^ unchecked((int)0x4B3951u));
        CaughtMeters = piece.Warhead.CaughtMeters;
        PinnedMeters = piece.Warhead.PinnedMeters;

        var downtown = KristiansandHeightSampler.ToWorldMeters(58.1464, 7.9956);
        Objective = new Vector3(
            downtown.X,
            terrain.Field.SampleHeight(downtown.X, downtown.Y),
            downtown.Y);

        var fort = KristiansandFort.ToWorldMeters();
        var fortY = terrain.Field.SampleHeight(fort.X, fort.Y);
        var ring = SimulationUnits.MinOpeningEngagementMeters(piece);
        var site = new GunSite(
            new Vector3(fort.X, fortY, fort.Y),
            openingElevationDegrees: 35f,
            columnLeadMeters: ring);
        GunX = site.Position.X;
        GunZ = site.Position.Z;
        TrailBearingDegrees = OpeningFirePlan.BearingDegrees(site.Position, Objective);

        PlaceDefenders();
        var sw = Stopwatch.StartNew();
        BuildSpawnCatalog();
        var afterSpawns = sw.ElapsedMilliseconds;
        ApplyRoundDifficulty();
        _nextWaveAt = 0f;
        SpawnWave();
        _nextWaveAt = _battleSeconds + WaveIntervalSeconds();

        RefreshNearest();
        var lay = OpeningFirePlan.Tune(
            terrain,
            site.Position,
            TrailBearingDegrees,
            site.OpeningElevationDegrees,
            _units,
            Objective,
            piece);
        OpeningElevationDegrees = lay.Elevation;
        OpeningChargeIndex = lay.Charge;
        OpeningAzimuthDegrees = lay.Bearing;
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Mission boot: seed={seed:x8} piece={piece.Id} spawns={afterSpawns}ms tune={sw.ElapsedMilliseconds - afterSpawns}ms total={sw.ElapsedMilliseconds}ms approaches={_spawnRoutes.Count}"));
    }

    public float CaughtMeters { get; }
    public float PinnedMeters { get; }

    public float GunX { get; }
    public float GunZ { get; }
    public Vector3 Objective { get; }
    public float TrailBearingDegrees { get; }
    public float OpeningElevationDegrees { get; }
    public int OpeningChargeIndex { get; }
    public float OpeningAzimuthDegrees { get; }
    public bool ObjectiveLost { get; private set; }
    public bool FratricideLoss { get; private set; }
    public int LastBracket { get; private set; }
    public int LastPinned { get; private set; }
    public float NearestMeters { get; private set; }
    public int Round => _round;
    public int SpawnPointCount => _spawnRoutes.Count;

    public string AvenueName => _approaches.Count == 0 ? "the primary roads" : string.Join(", ", _approaches);
    public int Remaining => _units.Count;
    public int Stopped => _wrecks.Count;
    public int DefendersAlive => _defenders.Count(d => d.Alive);
    /// <summary>
    /// Wave-survival victory: hold through late rounds with living defenders and roads jammed by wrecks.
    /// Once latched, Advance stops spawning and moving columns.
    /// </summary>
    public bool Held => _heldLatched || EvaluateHeld();

    public MissionOutcome Outcome
    {
        get
        {
            if (FratricideLoss)
                return MissionOutcome.Fratricide;
            if (ObjectiveLost)
                return MissionOutcome.ObjectiveLost;
            if (Held)
                return MissionOutcome.Held;
            return MissionOutcome.Open;
        }
    }

    public void Advance(float simSeconds, Vector3? incomingShell)
    {
        if (Outcome is not MissionOutcome.Open || simSeconds <= 0f)
            return;

        if (EvaluateHeld())
        {
            _heldLatched = true;
            return;
        }

        _battleSeconds += simSeconds;

        if (_battleSeconds >= _nextWaveAt)
        {
            if (Remaining == 0 && _waveSerial > 0)
            {
                _round++;
                ApplyRoundDifficulty();
            }

            SpawnWave();
            _nextWaveAt = _battleSeconds + WaveIntervalSeconds();
        }

        for (var i = _units.Count - 1; i >= 0; i--)
        {
            var unit = _units[i];
            var lane = _lanes[unit.Lane];
            var gap = GapAhead(unit, lane, out var wreckAhead);

            // Suppression only from ResolveImpact (burst) — never from a shell still in flight.
            if (unit.Scare > 0f)
                unit.Scare = MathF.Max(0f, unit.Scare - simSeconds);

            var suppressed = unit.Scare > 0f;
            var jitter = 0.92f + (unit.Id % 7) * 0.025f;
            var speed = (suppressed ? _columnSpeed * 0.35f : _columnSpeed) * jitter;
            var posture = suppressed ? "pinned" : "advancing";

            // Wrecks block the lane unless the vehicle is already passing around them.
            if (wreckAhead && gap < SpacingMeters * 0.9f && !unit.Passing)
            {
                if (gap < SpacingMeters * 0.55f && CanStartPass(unit, lane, gap))
                {
                    unit.Passing = true;
                    posture = "passing";
                }
                else
                {
                    unit.Posture = "blocked";
                    unit.Offset *= MathF.Max(0f, 1f - 2f * simSeconds);
                    _units[i] = unit;
                    continue;
                }
            }

            if (unit.Passing && gap > 160f)
                unit.Passing = false;
            else if (!wreckAhead && gap < SpacingMeters * 0.65f && gap < 90f && CanStartPass(unit, lane, gap))
            {
                unit.Passing = true;
                posture = "passing";
            }

            var desiredOffset = 0f;
            if (unit.Passing)
            {
                desiredOffset = (unit.Id & 1) == 0 ? 5.5f : -5.5f;
                posture = "passing";
            }
            else if (suppressed)
                desiredOffset = (unit.Id & 1) == 0 ? -4f : 4f;

            var step = 8f * simSeconds;
            unit.Offset += Math.Clamp(desiredOffset - unit.Offset, -step, step);
            unit.Offset = Math.Clamp(unit.Offset, -6.5f, 6.5f);

            // Passing vehicles crawl past a wreck; blocked ones already continued.
            if (wreckAhead && unit.Passing)
                speed *= 0.55f;

            var next = unit.Arc - lane.Away * speed * simSeconds;
            if (MathF.Abs(next - lane.CrossingArc) <= ObjectiveRadiusMeters)
            {
                Place(lane, ref unit);
                unit.Posture = posture;
                _units[i] = unit;
                ObjectiveLost = true;
                RefreshNearest();
                return;
            }

            var probe = lane.Line.At(next);
            if (!InPlayableMap(probe.X, probe.Y))
            {
                ArchiveUnit(i, ref unit);
                continue;
            }

            unit.Arc = next;
            Place(lane, ref unit);
            if (!InPlayableMap(unit.X, unit.Z))
                ArchiveUnit(i, ref unit);
            else
            {
                unit.Posture = posture;
                _units[i] = unit;
            }
        }

        RefreshNearest();
    }

    private bool EvaluateHeld() =>
        !ObjectiveLost
        && !FratricideLoss
        && DefendersAlive > 0
        && _round >= 8
        && Remaining == 0
        && ApproachesBlocked();

    private static bool CanStartPass(ColumnUnit unit, RouteLane lane, float gap) =>
        gap > 12f && MathF.Abs(unit.Offset) < 5.5f;

    public void ResolveImpact(Vector3 point)
    {
        LastBracket = 0;
        LastPinned = 0;
        for (var d = 0; d < _defenders.Count; d++)
        {
            var defender = _defenders[d];
            if (!defender.Alive)
                continue;

            var dx = defender.X - point.X;
            var dz = defender.Z - point.Z;
            if (MathF.Sqrt(dx * dx + dz * dz) > CaughtMeters)
                continue;

            defender.Alive = false;
            _defenders[d] = defender;
            FratricideLoss = true;
            ObjectiveLost = true;
        }

        for (var i = _units.Count - 1; i >= 0; i--)
        {
            var unit = _units[i];
            var dx = unit.X - point.X;
            var dz = unit.Z - point.Z;
            var distance = MathF.Sqrt(dx * dx + dz * dz);
            if (distance > PinnedMeters)
                continue;

            if (distance <= CaughtMeters)
            {
                ArchiveUnit(i, ref unit);
                LastBracket++;
                continue;
            }

            // Near-miss: suppress to ~⅓ speed — never freeze the column.
            unit.Hold = 0f;
            unit.Scare = MathF.Max(unit.Scare, _piece.Warhead.ScareSeconds);
            unit.Posture = "pinned";
            _units[i] = unit;
            LastPinned++;
        }

        RefreshNearest();
    }

    public void Draw(SilkWireBatch wires) =>
        DefenseSceneDraw.Draw(wires, _terrain, Objective, ObjectiveRadiusMeters, _units, _wrecks, _lanes, _defenders);

    public string HudLine() => Outcome switch
    {
        MissionOutcome.Fratricide => "FRATRICIDE — city lost",
        MissionOutcome.ObjectiveLost => "CROSSING LOST",
        MissionOutcome.Held => $"CITY HELD    {DefendersAlive} defenders    {Stopped} stopped",
        _ => $"ROUND {_round}  CROSSING {_waveSerial}  {Remaining}  {PostureLine()}  NEAREST {NearestMeters / 1000f:F1} KM",
    };

    public string Observation()
    {
        if (FratricideLoss)
            return "A defender was hit. The city is lost.";
        if (ObjectiveLost)
            return "The downtown hold is lost.";
        if (Held)
            return string.Create(CultureInfo.InvariantCulture, $"Kristiansand held. {DefendersAlive} defenders safe, {Stopped} hostiles stopped.");

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Defend Kvadraturen. Round {_round}, {Remaining} advancing on {AvenueName}, nearest {NearestMeters / 1000f:F2} km, {DefendersAlive} defenders.");
    }

    public string ResultPhrase()
    {
        if (ObjectiveLost)
            return "The crossing is lost.";
        if (Held)
            return string.Create(CultureInfo.InvariantCulture, $"The crossing held. {Stopped} stopped, the roads are blocked.");

        return string.Create(
            CultureInfo.InvariantCulture,
            $"The burst put {LastBracket} out of action and pinned {LastPinned}. {Remaining} still moving, nearest {NearestMeters / 1000f:F2} km.");
    }

    public Vector3 NearestAlive(Vector3 gun)
    {
        var nearest = Objective;
        var best = float.MaxValue;
        foreach (var unit in _units)
        {
            if (!unit.Alive)
                continue;

            var dx = unit.X - gun.X;
            var dz = unit.Z - gun.Z;
            var distance = dx * dx + dz * dz;
            if (distance >= best)
                continue;

            best = distance;
            nearest = new Vector3(unit.X, Height(unit.X, unit.Z), unit.Z);
        }

        return nearest;
    }

    public Vector3 BattleFocus(Vector3 gun)
    {
        var nearest = NearestAlive(gun);
        var mid = (gun + nearest) * 0.5f;
        mid.Y = MathF.Max(gun.Y, nearest.Y) + 70f;
        return mid;
    }

    public string PostureToken()
    {
        var pinned = 0;
        var passing = 0;
        var spreading = 0;
        foreach (var unit in _units)
        {
            if (!unit.Alive)
                continue;
            if (unit.Posture == "pinned")
                pinned++;
            else if (unit.Posture == "passing")
                passing++;
            else if (unit.Posture == "spreading")
                spreading++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{pinned}.{passing}.{spreading}");
    }

    public DefenderReport[] DefenderReports()
    {
        var rows = new DefenderReport[_defenders.Count];
        for (var i = 0; i < _defenders.Count; i++)
        {
            var defender = _defenders[i];
            rows[i] = new DefenderReport
            {
                Label = defender.Label,
                X = defender.X,
                Z = defender.Z,
                Alive = defender.Alive,
            };
        }

        return rows;
    }

    public ThreatReport[] Reports(Vector3 gun)
    {
        var reports = new ThreatReport[_units.Count + _wrecks.Count];
        var n = 0;
        void Add(ColumnUnit unit)
        {
            var dx = unit.X - gun.X;
            var dz = unit.Z - gun.Z;
            var bearing = MathF.Atan2(dz, dx) * (180f / MathF.PI);
            reports[n++] = new ThreatReport
            {
                Id = unit.Id,
                Road = _lanes[unit.Lane].Name,
                X = unit.X,
                Z = unit.Z,
                Alive = unit.Alive,
                InArc = MathF.Abs(OpeningFirePlan.DeltaDegrees(bearing, TrailBearingDegrees)) <= _piece.MaxTraverseDegrees,
                Posture = unit.Alive ? unit.Posture : "stopped",
                MetersFromGun = MathF.Sqrt(dx * dx + dz * dz),
                BearingDegrees = bearing,
                MetersToCrossing = Vector2.Distance(new Vector2(unit.X, unit.Z), new Vector2(Objective.X, Objective.Z)),
            };
        }

        foreach (var unit in _units)
            Add(unit);
        foreach (var wreck in _wrecks)
            Add(wreck);

        return reports;
    }

    private void BuildSpawnCatalog()
    {
        var goal = new Vector2(Objective.X, Objective.Z);
        var gun = new Vector2(GunX, GunZ);
        var ring = SimulationUnits.MinOpeningEngagementMeters(_piece);
        var routes = PrimaryRoadNetwork.BuildApproachSpawns(
            goal,
            gun,
            ring,
            SimulationUnits.ApproachSpawnCount,
            mapEastMeters: _terrain.EastMeters,
            mapNorthMeters: _terrain.NorthMeters,
            mapMarginMeters: MapMarginMeters);

        foreach (var route in routes)
        {
            if (IsUsableApproach(route))
                _spawnRoutes.Add(route);
        }

        if (_spawnRoutes.Count == 0)
        {
            // Absolute last resort: named geographic seeds still on the primary graph.
            foreach (var name in new[] { "Strai", "Hellemyr", "Søm", "Vågsbygd", "Møvik" })
            {
                var line = PrimaryRoadNetwork.RouteFromSeed(goal, ApproachSeed(name), name)
                    ?? PrimaryRoadNetwork.RouteToward(goal, ApproachBearing(name), name);
                if (line is not null && IsUsableApproach(line))
                    _spawnRoutes.Add(line);
            }
        }

        Console.WriteLine(
            $"OPFOR spawn catalog: {_spawnRoutes.Count} on-map primary approaches at ~{ring / 1000f:F2} km");
        ReshuffleSpawnDeck();
    }

    private bool IsUsableApproach(GroundPolyline line)
    {
        if (line.Points.Length < 2 || line.Length < 1_000f)
            return false;
        // Outer tip is the spawn mouth.
        if (!InPlayableMap(line.Points[0].X, line.Points[0].Y))
            return false;
        // Prefer land mouths — skip open-water NVDB artifacts.
        if (KristiansandSurface.KindAt(line.Points[0].X, line.Points[0].Y) == KristiansandSurfaceKind.Water)
            return false;
        return true;
    }

    private bool InPlayableMap(float x, float z) =>
        x >= MapMarginMeters
        && z >= MapMarginMeters
        && x <= _terrain.EastMeters - MapMarginMeters
        && z <= _terrain.NorthMeters - MapMarginMeters;

    private void ReshuffleSpawnDeck()
    {
        _spawnDeck.Clear();
        for (var i = 0; i < _spawnRoutes.Count; i++)
            _spawnDeck.Add(i);
        for (var i = _spawnDeck.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (_spawnDeck[i], _spawnDeck[j]) = (_spawnDeck[j], _spawnDeck[i]);
        }
    }

    private int DrawSpawnIndex()
    {
        if (_spawnRoutes.Count == 0)
            return -1;
        if (_spawnDeck.Count == 0)
            ReshuffleSpawnDeck();
        var last = _spawnDeck.Count - 1;
        var idx = _spawnDeck[last];
        _spawnDeck.RemoveAt(last);
        return idx;
    }

    private void ApplyRoundDifficulty()
    {
        // Round 1: baseline. Each later round: faster columns, denser packs, more simultaneous mouths.
        _columnSpeed = BaseColumnSpeedMetersPerSecond * (1f + 0.18f * (_round - 1));
    }

    private float WaveIntervalSeconds() =>
        Math.Clamp(95f - (_round - 1) * 12f, 35f, 95f);

    private int ColumnsThisWave() =>
        Math.Clamp(1 + (_round - 1) / 2 + (_waveSerial % 3 == 0 ? 1 : 0), 1, 4);

    private int VehiclesPerColumn() =>
        Math.Clamp(4 + _round, 4, 10);

    private void SpawnWave()
    {
        if (_spawnRoutes.Count == 0)
            return;

        _waveSerial++;
        var columns = ColumnsThisWave();
        var vehicles = VehiclesPerColumn();
        var spawned = 0;
        for (var c = 0; c < columns; c++)
        {
            var idx = DrawSpawnIndex();
            if (idx < 0)
                break;
            if (TrySpawnEngageableColumn(_spawnRoutes[idx], vehicles))
                spawned++;
        }

        // Retry leftover mouths if a route had no legal arcs this frame.
        for (var guard = 0; spawned < columns && guard < _spawnRoutes.Count; guard++)
        {
            var idx = DrawSpawnIndex();
            if (idx < 0)
                break;
            if (TrySpawnEngageableColumn(_spawnRoutes[idx], vehicles))
                spawned++;
        }
    }

    private string PostureLine()
    {
        var advancing = 0;
        var pinned = 0;
        var other = 0;
        foreach (var unit in _units)
        {
            if (!unit.Alive)
                continue;
            if (unit.Posture == "pinned")
                pinned++;
            else if (unit.Posture == "advancing")
                advancing++;
            else
                other++;
        }

        return other == 0
            ? $"{advancing} ADVANCING  {pinned} SUPPRESSED"
            : $"{advancing} ADVANCING  {pinned} SUPPRESSED  {other} OTHER";
    }

    private bool ApproachesBlocked()
    {
        // With no living columns (Held already requires Remaining == 0), wrecks mean the roads are jammed.
        if (_wrecks.Count == 0)
            return false;

        var lanesWithWrecks = 0;
        var seen = new HashSet<int>();
        foreach (var wreck in _wrecks)
        {
            if (seen.Add(wreck.Lane))
                lanesWithWrecks++;
        }

        return lanesWithWrecks >= 1;
    }

    private float GapAhead(ColumnUnit unit, RouteLane lane, out bool wreckAhead)
    {
        wreckAhead = false;
        var best = float.MaxValue;
        var dead = false;

        void Consider(ColumnUnit other, bool isWreck)
        {
            if (other.Lane != unit.Lane || other.Id == unit.Id)
                return;

            var toward = (unit.Arc - other.Arc) * lane.Away;
            if (toward <= 8f || toward >= best)
                return;

            best = toward;
            dead = isWreck;
        }

        foreach (var other in _units)
            Consider(other, isWreck: false);
        foreach (var wreck in _wrecks)
            Consider(wreck, isWreck: true);

        wreckAhead = dead;
        return best;
    }

    private void ArchiveUnit(int index, ref ColumnUnit unit)
    {
        unit.Alive = false;
        unit.Posture = "stopped";
        unit.Passing = false;
        unit.Scare = 0f;
        unit.Offset = 0f;
        _wrecks.Add(unit);
        if (index >= 0 && index < _units.Count)
            _units.RemoveAt(index);

        while (_wrecks.Count > MaxTrackedWrecks)
            _wrecks.RemoveAt(0);
    }

    private void Place(RouteLane lane, ref ColumnUnit unit)
    {
        var point = lane.Line.At(unit.Arc);
        var probe = lane.Line.At(unit.Arc - lane.Away * 16f);
        var forward = probe - point;
        if (forward.LengthSquared() < 1f)
            forward = new Vector2(lane.Away, 0f);

        forward = Vector2.Normalize(forward);
        var side = new Vector2(-forward.Y, forward.X);
        var placed = point + side * unit.Offset;
        unit.X = Math.Clamp(placed.X, MapMarginMeters, _terrain.EastMeters - MapMarginMeters);
        unit.Z = Math.Clamp(placed.Y, MapMarginMeters, _terrain.NorthMeters - MapMarginMeters);
    }

    /// <summary>
    /// Place a column on a primary-road polyline. Lead tip prefers the spawn ring
    /// (1.25× HE min from the gun); vehicles march toward downtown along the road.
    /// </summary>
    private bool TrySpawnEngageableColumn(GroundPolyline template, int count)
    {
        // Fresh copy so concurrent waves on the same approach don't share mutable lane state oddly.
        var line = new GroundPolyline(template.Name, template.Kind, (Vector2[])template.Points.Clone());
        var gun = new Vector2(GunX, GunZ);
        var goal = new Vector2(Objective.X, Objective.Z);
        var ring = SimulationUnits.MinOpeningEngagementMeters(_piece);
        var minOpen = ring * 0.92f;
        var maxOpen = MathF.Min(SimulationUnits.MaxCityApproachMeters, ring * 1.4f);
        var minCity = ObjectiveRadiusMeters + 900f;

        var legal = new List<(float Arc, float RingErr, float ObjDist)>();
        for (var arc = 40f; arc <= line.Length - 40f; arc += 30f)
        {
            var p = line.At(arc);
            if (!InPlayableMap(p.X, p.Y))
                continue;
            if (KristiansandSurface.KindAt(p.X, p.Y) == KristiansandSurfaceKind.Water)
                continue;
            var gd = Vector2.Distance(p, gun);
            if (gd < minOpen || gd > maxOpen)
                continue;
            var od = Vector2.Distance(p, goal);
            if (od < minCity)
                continue;
            legal.Add((arc, MathF.Abs(gd - ring), od));
        }

        if (legal.Count < 2)
            return false;

        // Lead tip ≈ ring; pack trails away from the city along the road.
        legal.Sort(static (a, b) =>
        {
            var c = a.RingErr.CompareTo(b.RingErr);
            return c != 0 ? c : a.ObjDist.CompareTo(b.ObjDist);
        });
        var leadArc = legal[0].Arc;

        var aPlus = Math.Clamp(leadArc + 80f, 40f, line.Length - 40f);
        var aMinus = Math.Clamp(leadArc - 80f, 40f, line.Length - 40f);
        var away = Vector2.Distance(line.At(aPlus), goal) >= Vector2.Distance(line.At(aMinus), goal) ? 1 : -1;

        var spacing = MathF.Max(45f, SpacingMeters - (_round - 1) * 4f);
        var pack = (count - 1) * spacing;
        // If the tip has no room for the pack farther out, slide the lead toward the city.
        var tail = leadArc + away * pack;
        if (tail < 40f || tail > line.Length - 40f)
            leadArc = Math.Clamp(leadArc - away * pack, 40f, line.Length - 40f);

        var end = line.Points[^1];
        var start = line.Points[0];
        var crossingArc = Vector2.Distance(end, goal) <= Vector2.Distance(start, goal)
            ? line.Length - 40f
            : 40f;

        var lane = _lanes.Count;
        _lanes.Add(new RouteLane
        {
            Line = line,
            CrossingArc = crossingArc,
            Away = away,
            Name = line.Name,
            Ribbon = DefenseSceneDraw.BuildRibbon(_terrain, line),
        });
        if (!_approaches.Contains(line.Name))
            _approaches.Add(line.Name);

        var before = _units.Count;
        var used = new HashSet<int>();
        var nextId = _units.Count + _wrecks.Count + 1;
        for (var i = 0; i < count; i++)
        {
            var arc = Math.Clamp(leadArc + away * i * spacing, 40f, line.Length - 40f);
            var key = (int)MathF.Round(arc);
            if (!used.Add(key))
                continue;

            var point = line.At(arc);
            if (!InPlayableMap(point.X, point.Y))
                continue;
            if (KristiansandSurface.KindAt(point.X, point.Y) == KristiansandSurfaceKind.Water)
                continue;
            if (Vector2.Distance(point, gun) < minOpen * 0.75f)
                continue;

            var unit = new ColumnUnit
            {
                Id = nextId++,
                Lane = lane,
                Arc = arc,
                Alive = true,
                Posture = "advancing",
            };
            Place(_lanes[lane], ref unit);
            _units.Add(unit);
        }

        return _units.Count > before;
    }

    private static float ApproachBearing(string roadToken) => roadToken switch
    {
        "Strai" => 128f,
        "Hellemyr" => 165f,
        "Søm" => 12f,
        "Møvik" or "Movik" => 235f,
        "Vågsbygd" or "Vagsbygd" => 245f,
        _ => 90f,
    };

    private static Vector2 ApproachSeed(string roadToken)
    {
        var (lat, lon) = roadToken switch
        {
            "Strai" => (58.1893, 7.92826),
            "Hellemyr" => (58.1680, 7.9450),
            "Søm" => (58.1520, 8.0780),
            "Møvik" or "Movik" => (58.1250, 7.9550),
            "Vågsbygd" or "Vagsbygd" => (58.1320, 7.9480),
            _ => (58.1700, 7.9600),
        };
        return KristiansandHeightSampler.ToWorldMeters(lat, lon);
    }

    private void PlaceDefenders()
    {
        AddDefender("D1", DefenderAt(270f, 240f));
        AddDefender("D2", DefenderAt(180f, 220f));
        AddDefender("D3", DefenderAt(0f, 220f));
        AddDefender("D4", DefenderAt(90f, 200f));
    }

    private Vector2 DefenderAt(float bearingDegrees, float meters)
    {
        var rad = bearingDegrees * (MathF.PI / 180f);
        var seed = new Vector2(
            Objective.X + MathF.Cos(rad) * meters,
            Objective.Z + MathF.Sin(rad) * meters);
        if (PrimaryRoadNetwork.Snap(seed) is { } snapped
            && Vector2.Distance(snapped, seed) < 140f)
            return snapped;
        return seed;
    }

    private void AddDefender(string label, Vector2 at)
    {
        _defenders.Add(new DefenderUnit
        {
            Label = label,
            X = at.X,
            Z = at.Y,
            YawDegrees = OpeningFirePlan.BearingDegrees(new Vector3(GunX, 0f, GunZ), new Vector3(at.X, 0f, at.Y)),
            Alive = true,
        });
    }

    private void RefreshNearest()
    {
        var nearest = float.MaxValue;
        foreach (var unit in _units)
        {
            if (!unit.Alive)
                continue;

            var dx = unit.X - Objective.X;
            var dz = unit.Z - Objective.Z;
            nearest = MathF.Min(nearest, MathF.Sqrt(dx * dx + dz * dz));
        }

        NearestMeters = nearest == float.MaxValue ? 0f : nearest;
    }

    private float Height(float x, float z) => _terrain.Field.SampleHeight(x, z);
}
