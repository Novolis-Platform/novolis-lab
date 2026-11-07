using System.Globalization;
using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// Defense of Kristiansand: downtown defenders hold Kvadraturen while hostile columns
/// advance on primary roads. Burst distances are a game rule, not a weapon table.
/// </summary>
internal sealed class KristiansandDefenseMission
{
    public const float CaughtMeters = 200f;
    public const float PinnedMeters = 340f;
    public const float ObjectiveRadiusMeters = 180f;
    public const float ColumnSpeedMetersPerSecond = 4.5f;
    public const int ColumnCount = 6;

    private const float SpacingMeters = 180f;

    private readonly TerrainWorld _terrain;
    private readonly float _spawnLead;
    private readonly List<string> _approaches = [];
    private readonly List<RouteLane> _lanes = [];
    private readonly List<ColumnUnit> _units = [];
    private readonly List<DefenderUnit> _defenders = [];
    private readonly List<PendingWave> _pendingWaves = [];
    private float _battleSeconds;

    public KristiansandDefenseMission(TerrainWorld terrain)
    {
        _terrain = terrain;
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;

        var downtown = KristiansandHeightSampler.ToWorldMeters(58.1464, 7.9956);
        Objective = new Vector3(
            downtown.X,
            terrain.Field.SampleHeight(downtown.X, downtown.Y),
            downtown.Y);

        var fort = KristiansandFort.ToWorldMeters();
        var fortY = terrain.Field.SampleHeight(fort.X, fort.Y);
        var site = new GunSite(
            new Vector3(fort.X, fortY, fort.Y),
            openingElevationDegrees: 22f,
            columnLeadMeters: Math.Clamp(
                OpeningFirePlan.Horizontal(new Vector3(fort.X, fortY, fort.Y), Objective) * 0.55f,
                1_800f,
                5_500f));
        GunX = site.Position.X;
        GunZ = site.Position.Z;
        _spawnLead = site.ColumnLeadMeters;
        TrailBearingDegrees = OpeningFirePlan.BearingDegrees(site.Position, Objective);

        PlaceDefenders();
        QueueWaves();
        SpawnWave("Strai");
        if (_units.Count == 0)
            SpawnOtherRoads();
        if (_units.Count == 0)
            SpawnStraight(east, north);

        RefreshNearest();
        var lay = OpeningFirePlan.Tune(terrain, site.Position, TrailBearingDegrees, site.OpeningElevationDegrees, _units, Objective);
        OpeningElevationDegrees = lay.Elevation;
        OpeningChargeIndex = lay.Charge;
        OpeningAzimuthDegrees = lay.Bearing;
    }

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

    public string AvenueName => _approaches.Count == 0 ? "the other roads" : string.Join(", ", _approaches);
    public int Remaining => _units.Count(unit => unit.Alive);
    public int Stopped => _units.Count - Remaining;
    public int DefendersAlive => _defenders.Count(d => d.Alive);
    public bool Held => !ObjectiveLost && !FratricideLoss && DefendersAlive > 0 && ApproachesBlocked();

    public void Advance(float simSeconds, Vector3? incomingShell)
    {
        if (ObjectiveLost || FratricideLoss || Held || simSeconds <= 0f)
            return;

        _battleSeconds += simSeconds;
        for (var w = 0; w < _pendingWaves.Count; w++)
        {
            var wave = _pendingWaves[w];
            if (wave.Spawned || _battleSeconds < wave.AtSeconds)
                continue;

            wave.Spawned = true;
            _pendingWaves[w] = wave;
            SpawnWave(wave.RoadToken);
        }

        for (var i = 0; i < _units.Count; i++)
        {
            var unit = _units[i];
            if (!unit.Alive)
                continue;

            var lane = _lanes[unit.Lane];
            var gap = GapAhead(unit, lane, out var wreckAhead);
            var speed = ColumnSpeedMetersPerSecond;
            var posture = "advancing";

            if (unit.Hold > 0f)
            {
                unit.Hold = MathF.Max(0f, unit.Hold - simSeconds);
                speed = 0f;
                posture = "holding";
            }
            else if (!wreckAhead && gap < SpacingMeters)
            {
                speed = gap < SpacingMeters * 0.6f ? 0f : ColumnSpeedMetersPerSecond * 0.35f;
                posture = "holding";
            }
            else if (wreckAhead && gap < SpacingMeters)
            {
                speed = 0f;
                posture = "holding";
            }

            if (incomingShell is Vector3 shell && ShellArriving(unit, shell))
            {
                unit.Hold = MathF.Max(unit.Hold, 2.4f);
                speed = 0f;
                posture = "holding";
            }

            if (unit.Scare > 0f)
            {
                unit.Scare = MathF.Max(0f, unit.Scare - simSeconds);
                if (speed > 0f)
                    speed *= 0.35f;
                if (posture == "advancing")
                    posture = "pinned";
            }

            if (unit.Passing && gap > 160f)
                unit.Passing = false;

            var desiredOffset = 0f;
            if (unit.Passing)
                desiredOffset = (unit.Id & 1) == 0 ? 5.5f : -5.5f;
            else if (unit.Scare > 0f)
                desiredOffset = (unit.Id & 1) == 0 ? -4f : 4f;

            var step = 8f * simSeconds;
            unit.Offset += Math.Clamp(desiredOffset - unit.Offset, -step, step);
            unit.Offset = Math.Clamp(unit.Offset, -6.5f, 6.5f);

            if (speed > 0f)
            {
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

                unit.Arc = next;
            }

            Place(lane, ref unit);
            unit.Posture = posture;
            _units[i] = unit;
        }

        RefreshNearest();
    }

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

        for (var i = 0; i < _units.Count; i++)
        {
            var unit = _units[i];
            if (!unit.Alive)
                continue;

            var dx = unit.X - point.X;
            var dz = unit.Z - point.Z;
            var distance = MathF.Sqrt(dx * dx + dz * dz);
            if (distance > PinnedMeters)
                continue;

            if (distance <= CaughtMeters)
            {
                unit.Alive = false;
                unit.Posture = "stopped";
                unit.Offset = 0f;
                unit.Scare = 0f;
                _units[i] = unit;
                LastBracket++;
                continue;
            }

            unit.Hold = MathF.Max(unit.Hold, 18f);
            unit.Scare = 16f;
            unit.Posture = "pinned";
            _units[i] = unit;
            LastPinned++;
        }

        RefreshNearest();
    }

    public void Draw(SilkWireBatch wires) =>
        DefenseSceneDraw.Draw(wires, _terrain, Objective, ObjectiveRadiusMeters, _units, _lanes, _defenders);

    public string HudLine()
    {
        if (ObjectiveLost)
            return "crossing lost";
        if (Held)
            return $"city held    {DefendersAlive} defenders    {Stopped} stopped";

        return $"crossing    {Remaining}    {PostureLine()}    nearest {NearestMeters / 1000f:F1} km";
    }

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
            $"Defend Kvadraturen. {Remaining} advancing on {AvenueName}, nearest {NearestMeters / 1000f:F2} km, {DefendersAlive} defenders.");
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
        var holding = 0;
        var passing = 0;
        var spreading = 0;
        foreach (var unit in _units)
        {
            if (!unit.Alive)
                continue;
            if (unit.Posture == "holding")
                holding++;
            else if (unit.Posture == "passing")
                passing++;
            else if (unit.Posture == "spreading")
                spreading++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{holding}.{passing}.{spreading}");
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
        var reports = new ThreatReport[_units.Count];
        for (var i = 0; i < _units.Count; i++)
        {
            var unit = _units[i];
            var dx = unit.X - gun.X;
            var dz = unit.Z - gun.Z;
            var bearing = MathF.Atan2(dz, dx) * (180f / MathF.PI);
            reports[i] = new ThreatReport
            {
                Id = unit.Id,
                Road = _lanes[unit.Lane].Name,
                X = unit.X,
                Z = unit.Z,
                Alive = unit.Alive,
                InArc = MathF.Abs(OpeningFirePlan.DeltaDegrees(bearing, TrailBearingDegrees)) <= 22.5f,
                Posture = unit.Alive ? unit.Posture : "stopped",
                MetersFromGun = MathF.Sqrt(dx * dx + dz * dz),
                BearingDegrees = bearing,
                MetersToCrossing = Vector2.Distance(new Vector2(unit.X, unit.Z), new Vector2(Objective.X, Objective.Z)),
            };
        }

        return reports;
    }

    private string PostureLine()
    {
        var advancing = 0;
        var holding = 0;
        var other = 0;
        foreach (var unit in _units)
        {
            if (!unit.Alive)
                continue;
            if (unit.Posture == "holding")
                holding++;
            else if (unit.Posture == "advancing")
                advancing++;
            else
                other++;
        }

        return other == 0
            ? $"{advancing} advancing  {holding} holding"
            : $"{advancing} advancing  {holding} holding  {other} off the road";
    }

    private bool ApproachesBlocked()
    {
        if (Stopped == 0)
            return false;

        foreach (var unit in _units)
        {
            if (unit.Alive && !HasWreckCloser(unit))
                return false;
        }

        return true;
    }

    private bool HasWreckCloser(ColumnUnit unit)
    {
        var lane = _lanes[unit.Lane];
        foreach (var other in _units)
        {
            if (other.Alive || other.Lane != unit.Lane)
                continue;

            var toward = (unit.Arc - other.Arc) * lane.Away;
            if (toward > 8f)
                return true;
        }

        return false;
    }

    private float GapAhead(ColumnUnit unit, RouteLane lane, out bool wreckAhead)
    {
        wreckAhead = false;
        var best = float.MaxValue;
        var dead = false;
        foreach (var other in _units)
        {
            if (other.Lane != unit.Lane || other.Id == unit.Id)
                continue;

            var toward = (unit.Arc - other.Arc) * lane.Away;
            if (toward <= 8f || toward >= best)
                continue;

            best = toward;
            dead = !other.Alive;
        }

        wreckAhead = dead;
        return best;
    }

    private bool ShellArriving(ColumnUnit unit, Vector3 shell)
    {
        var dx = shell.X - unit.X;
        var dz = shell.Z - unit.Z;
        if (dx * dx + dz * dz > 260f * 260f)
            return false;

        return shell.Y - Height(unit.X, unit.Z) < 160f;
    }

    private static void Place(RouteLane lane, ref ColumnUnit unit)
    {
        var point = lane.Line.At(unit.Arc);
        var probe = lane.Line.At(unit.Arc - lane.Away * 16f);
        var forward = probe - point;
        if (forward.LengthSquared() < 1f)
            forward = new Vector2(lane.Away, 0f);

        forward = Vector2.Normalize(forward);
        var side = new Vector2(-forward.Y, forward.X);
        var placed = point + side * unit.Offset;
        unit.X = placed.X;
        unit.Z = placed.Y;
    }

    private void AddColumn(GroundPolyline line, float crossingArc, int away, int count, float lead)
    {
        var lane = _lanes.Count;
        _lanes.Add(new RouteLane
        {
            Line = line,
            CrossingArc = crossingArc,
            Away = away,
            Name = line.Name,
        });
        if (!_approaches.Contains(line.Name))
            _approaches.Add(line.Name);
        for (var i = 0; i < count; i++)
        {
            var arc = Math.Clamp(crossingArc + away * (lead + i * SpacingMeters), 40f, line.Length - 40f);
            if (MathF.Abs(arc - crossingArc) < 160f)
                continue;

            var point = line.At(arc);
            _units.Add(new ColumnUnit
            {
                Id = _units.Count + 1,
                Lane = lane,
                Arc = arc,
                X = point.X,
                Z = point.Y,
                Alive = true,
                Posture = "advancing",
            });
        }
    }

    private void QueueWaves()
    {
        _pendingWaves.Add(new PendingWave("Møvik", 300f, false));
        _pendingWaves.Add(new PendingWave("Hellemyr", 600f, false));
        _pendingWaves.Add(new PendingWave("Søm", 900f, false));
    }

    private void SpawnWave(string roadToken)
    {
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        if (TrySpawnPrimaryRoadColumn(roadToken))
            return;

        SpawnBearingColumn(east, north, ApproachBearing(roadToken), roadToken);
    }

    /// <summary>
    /// Math bearing from Kvadraturen toward the named approach (0° = east, 90° = north).
    /// Used only when primary-road seed routing fails.
    /// </summary>
    private static float ApproachBearing(string roadToken) => roadToken switch
    {
        "Strai" => 128f,
        "Hellemyr" => 165f,
        "Søm" => 12f,
        "Møvik" or "Movik" => 235f,
        "Vågsbygd" or "Vagsbygd" => 245f,
        _ => 90f,
    };

    /// <summary>WGS84 seeds for inbound approaches (Kartverket / OSM place points).</summary>
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

    private bool TrySpawnPrimaryRoadColumn(string name)
    {
        var goal = new Vector2(Objective.X, Objective.Z);
        var line = PrimaryRoadNetwork.RouteFromSeed(goal, ApproachSeed(name), name)
            ?? PrimaryRoadNetwork.RouteToward(goal, ApproachBearing(name), name);
        if (line is null)
            return false;

        var end = line.Points[^1];
        var start = line.Points[0];
        var endDist = Vector2.Distance(end, goal);
        var startDist = Vector2.Distance(start, goal);
        // March from the far (seed) end toward downtown — park the column near the approach origin.
        var crossingArc = endDist <= startDist ? line.Length - 40f : 40f;
        var away = endDist <= startDist ? -1 : 1;
        var columnSpan = (ColumnCount - 1) * SpacingMeters;
        var rearClearance = 240f;
        var lead = crossingArc - rearClearance - columnSpan;
        if (away > 0)
            lead = line.Length - crossingArc - rearClearance - columnSpan;
        lead = Math.Clamp(lead, 600f, MathF.Max(800f, line.Length - 200f));
        var before = _units.Count;
        AddColumn(line, crossingArc, away, ColumnCount, lead);
        return _units.Count > before;
    }

    private void PlaceDefenders()
    {
        // Four downtown mouths: south harbor, west Grim, east Lund, north Strai/Baneheia.
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
        // Snap only when the road node stays on the same mouth (avoid collapsing all four onto one junction).
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

    private void SpawnOtherRoads()
    {
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        if (!TrySpawnPrimaryRoadColumn("Strai"))
            SpawnBearingColumn(east, north, ApproachBearing("Strai"), "Strai");
        if (!TrySpawnPrimaryRoadColumn("Vågsbygd"))
            SpawnBearingColumn(east, north, ApproachBearing("Vågsbygd"), "Vågsbygd");
        if (!TrySpawnPrimaryRoadColumn("Søm"))
            SpawnBearingColumn(east, north, ApproachBearing("Søm"), "Søm");
    }

    private void SpawnBearingColumn(float east, float north, float approachBearingDegrees, string name)
    {
        var rad = approachBearingDegrees * (MathF.PI / 180f);
        var fromX = MathF.Cos(rad);
        var fromZ = MathF.Sin(rad);
        var far = _spawnLead + ColumnCount * SpacingMeters + 600f;
        var start = new Vector2(
            Math.Clamp(Objective.X + fromX * far, 40f, east - 40f),
            Math.Clamp(Objective.Z + fromZ * far, 40f, north - 40f));
        var end = new Vector2(
            Math.Clamp(Objective.X - fromX * 120f, 40f, east - 40f),
            Math.Clamp(Objective.Z - fromZ * 120f, 40f, north - 40f));
        var line = new GroundPolyline(name, "road", [start, end]);
        var length = Vector2.Distance(start, end);
        if (length < 400f)
            return;

        AddColumn(line, length - 120f, -1, ColumnCount, Math.Clamp(length * 0.35f, 900f, 1_800f));
    }

    private void SpawnStraight(float east, float north)
    {
        var dx = Objective.X - GunX;
        var dz = Objective.Z - GunZ;
        var length = MathF.Max(1f, MathF.Sqrt(dx * dx + dz * dz));
        var dirX = dx / length;
        var dirZ = dz / length;
        var far = _spawnLead + ColumnCount * SpacingMeters + 400f;
        var line = new GroundPolyline(
            "column",
            "road",
            [
                new Vector2(Math.Clamp(Objective.X - dirX * 200f, 40f, east - 40f), Math.Clamp(Objective.Z - dirZ * 200f, 40f, north - 40f)),
                new Vector2(Math.Clamp(Objective.X + dirX * far, 40f, east - 40f), Math.Clamp(Objective.Z + dirZ * far, 40f, north - 40f)),
            ]);
        AddColumn(line, 200f, 1, ColumnCount, _spawnLead);
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
