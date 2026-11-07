using System.Drawing;
using System.Globalization;
using System.Numerics;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator.Game;

/// <summary>
/// The Defense of Kristiansand on the real southern Norway grid.
/// Downtown defenders must survive; hostile columns advance on staggered road spawns.
/// Burst distances are a game rule, not a weapon table.
/// </summary>
internal sealed class KristiansandDefenseMission
{
    public const float BracketMeters = 90f;

    /// <summary>Vehicles this close to the impact are out of action.</summary>
    public const float CaughtMeters = 200f;

    /// <summary>Vehicles this close halt and then crawl. They do not speed up.</summary>
    public const float PinnedMeters = 340f;
    public const float ObjectiveRadiusMeters = 180f;
    /// <summary>About 16 km/h. A careful approach, slow enough that plunging fire can reach each road.</summary>
    public const float ColumnSpeedMetersPerSecond = 4.5f;
    public const int ColumnCount = 6;

    private const float SpacingMeters = 180f;

    private readonly TerrainWorld _terrain;
    private readonly float _columnLead;
    private readonly GroundPolyline? _road;
    private readonly float _crossingArc;
    private readonly float _spawnLead;
    private readonly List<string> _approaches = [];
    private readonly List<RouteLane> _lanes = [];
    private readonly List<ColumnUnit> _units = [];
    private readonly List<DefenderUnit> _defenders = [];
    private readonly List<PendingWave> _pendingWaves = [];
    private float _battleSeconds;

    public bool FratricideLoss { get; private set; }

    public int DefendersAlive => _defenders.Count(d => d.Alive);

    public KristiansandDefenseMission(TerrainWorld terrain)
    {
        _terrain = terrain;
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        // OSM polylines were baked for a wider WGS84 box — do not use them for objective/georef.
        _road = null;
        _crossingArc = 0f;
        var downtown = KristiansandHeightSampler.ToWorldMeters(58.1464, 7.9956);
        Objective = new Vector3(
            downtown.X,
            terrain.Field.SampleHeight(downtown.X, downtown.Y),
            downtown.Y);

        // Fixed battery: Odderøya old fort (Odderøyveien 23), snapped onto the fort hill.
        var fort = KristiansandFort.ToWorldMeters();
        var fortY = terrain.Field.SampleHeight(fort.X, fort.Y);
        var site = new GunSite(
            new Vector3(fort.X, fortY, fort.Y),
            openingElevationDegrees: 22f,
            columnLeadMeters: Math.Clamp(Horizontal(new Vector3(fort.X, fortY, fort.Y), Objective) * 0.55f, 1_800f, 5_500f));
        GunX = site.Position.X;
        GunZ = site.Position.Z;
        _columnLead = site.ColumnLeadMeters;
        TrailBearingDegrees = BearingDegrees(site.Position, Objective);
        _spawnLead = _columnLead;
        PlaceDefenders();
        QueueWaves();
        SpawnWave("Strai");
        if (_units.Count == 0)
            SpawnOtherRoads();
        if (_units.Count == 0)
            SpawnStraight(east, north);

        RefreshNearest();
        var lay = TuneOpening(terrain, site.Position, site.OpeningElevationDegrees);
        OpeningElevationDegrees = lay.Elevation;
        OpeningChargeIndex = lay.Charge;
        OpeningAzimuthDegrees = lay.Bearing;
    }

    public float GunX { get; }

    public float GunZ { get; }

    public Vector3 Objective { get; }

    public float TrailBearingDegrees { get; }

    public string AvenueName => _approaches.Count == 0 ? "the other roads" : string.Join(", ", _approaches);

    /// <summary>Opening quadrant in the upper register, so the shell clears the crest.</summary>
    public float OpeningElevationDegrees { get; }

    public int OpeningChargeIndex { get; }

    /// <summary>World bearing of the opening lay, inside the trail fan, on the column nearest the crossing.</summary>
    public float OpeningAzimuthDegrees { get; }

    public bool ObjectiveLost { get; private set; }

    public int LastBracket { get; private set; }

    public int LastPinned { get; private set; }

    public int Remaining => _units.Count(unit => unit.Alive);

    public int Stopped => _units.Count - Remaining;

    public bool Held => !ObjectiveLost && !FratricideLoss && DefendersAlive > 0 && ApproachesBlocked();

    public float NearestMeters { get; private set; }

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

            // Stay on the carriageway — primary roads only, small lateral weave.
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
            var distance = MathF.Sqrt(dx * dx + dz * dz);
            if (distance > CaughtMeters)
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

    public string HudLine(bool paused)
    {
        if (paused)
            return "flat check    column paused";
        if (ObjectiveLost)
            return "crossing lost";
        if (Held)
            return $"city held    {DefendersAlive} defenders    {Stopped} stopped";

        return $"crossing    {Remaining}    {PostureLine()}    nearest {NearestMeters / 1000f:F1} km";
    }

    public string Observation(bool paused)
    {
        if (paused)
            return "Column paused on the flat check.";
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

    public void Draw(SilkWireBatch wires)
    {
        var ground = ObjectiveHeight(Objective.X, Objective.Z);
        var mark = new Vector3(Objective.X, ground, Objective.Z);
        var copper = Color.FromArgb(255, 232, 196, 120);
        wires.Line(mark, mark + Vector3.UnitY * 120f, copper);
        DrawRing(wires, mark + Vector3.UnitY * 2f, ObjectiveRadiusMeters, copper);

        var route = Color.FromArgb(200, 255, 120, 48);
        var hostile = Color.FromArgb(255, 255, 48, 32);
        var hostileHot = Color.FromArgb(255, 255, 210, 64);
        var wreck = Color.FromArgb(180, 130, 120, 110);
        var friendly = Color.FromArgb(255, 72, 210, 120);
        foreach (var unit in _units)
        {
            var y = ObjectiveHeight(unit.X, unit.Z);
            var lift = unit.Alive ? 4.5f : 2f;
            var at = new Vector3(unit.X, y + lift, unit.Z);
            var lane = _lanes[unit.Lane];
            var yaw = LaneYaw(lane, unit.Arc);
            var color = unit.Alive
                ? (unit.Posture is "holding" or "pinned" ? hostileHot : hostile)
                : wreck;
            NatoSymbolDraw.HostileMechanized(wires, at, yaw, 42f, color, wreck: !unit.Alive);
        }

        // Draw the active primary-road approaches so columns read as road-bound.
        foreach (var lane in _lanes)
            DrawLaneRibbon(wires, lane, route);

        foreach (var defender in _defenders)
        {
            if (!defender.Alive)
                continue;

            var at = new Vector3(defender.X, ObjectiveHeight(defender.X, defender.Z) + 3f, defender.Z);
            NatoSymbolDraw.FriendlyInfantry(wires, at, defender.YawDegrees, 22f, friendly);
        }
    }

    private void DrawLaneRibbon(SilkWireBatch wires, RouteLane lane, Color color)
    {
        const float step = 80f;
        Vector3? previous = null;
        for (var arc = 0f; arc <= lane.Line.Length; arc += step)
        {
            var p = lane.Line.At(Math.Min(arc, lane.Line.Length));
            var at = new Vector3(p.X, ObjectiveHeight(p.X, p.Y) + 2.5f, p.Y);
            if (previous is { } from)
                wires.Line(from, at, color);
            previous = at;
        }
    }

    private float ObjectiveHeight(float x, float z) => _terrain.Field.SampleHeight(x, z);

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
            nearest = new Vector3(unit.X, ObjectiveHeight(unit.X, unit.Z), unit.Z);
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

        var ground = ObjectiveHeight(unit.X, unit.Z);
        return shell.Y - ground < 160f;
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

    private static Color PostureColor(string posture) => posture switch
    {
        "holding" => Color.FromArgb(255, 232, 176, 64),
        "pinned" => Color.FromArgb(255, 150, 136, 110),
        "passing" => Color.FromArgb(255, 120, 176, 210),
        "spreading" => Color.FromArgb(255, 230, 120, 70),
        _ => Color.FromArgb(255, 196, 72, 52),
    };

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
        var bearing = ApproachBearing(roadToken);
        if (TrySpawnPrimaryRoadColumn(bearing, roadToken))
            return;

        // Fallback only if primary-road graph failed to load.
        SpawnBearingColumn(east, north, bearing, roadToken);
    }

    private static float ApproachBearing(string roadToken) => roadToken switch
    {
        "Strai" => 200f,
        "Hellemyr" => 240f,
        "Søm" => 110f,
        "Møvik" or "Movik" => 255f,
        "Vågsbygd" or "Vagsbygd" => 260f,
        _ => 180f,
    };

    private bool TrySpawnPrimaryRoadColumn(float approachBearingDegrees, string name)
    {
        var goal = new Vector2(Objective.X, Objective.Z);
        var line = PrimaryRoadNetwork.RouteToward(goal, approachBearingDegrees, name);
        if (line is null)
            return false;

        // Crossing is the end nearest downtown; columns advance toward decreasing distance to goal.
        var end = line.Points[^1];
        var start = line.Points[0];
        var endDist = Vector2.Distance(end, goal);
        var startDist = Vector2.Distance(start, goal);
        var crossingArc = endDist <= startDist ? line.Length - 40f : 40f;
        var away = endDist <= startDist ? -1 : 1;
        var lead = Math.Clamp(line.Length * 0.28f, 1_200f, 3_500f);
        var before = _units.Count;
        AddColumn(line, crossingArc, away, ColumnCount, lead);
        var spawned = _units.Count > before;
        if (spawned)
            Console.WriteLine($"[ArtillerySimulator] primary road column '{name}'  {line.Length / 1000f:F1} km  {line.Points.Length} nodes");
        return spawned;
    }

    private void PlaceDefenders()
    {
        var center = new Vector2(Objective.X, Objective.Z);
        AddDefender("D1", center + new Vector2(0f, -220f));
        AddDefender("D2", center + new Vector2(-200f, 0f));
        AddDefender("D3", center + new Vector2(180f, 0f));
        AddDefender("D4", center + new Vector2(0f, 160f));
    }

    private void AddDefender(string label, Vector2 at)
    {
        _defenders.Add(new DefenderUnit
        {
            Label = label,
            X = at.X,
            Z = at.Y,
            YawDegrees = BearingDegrees(new Vector3(GunX, 0f, GunZ), new Vector3(at.X, 0f, at.Y)),
            Alive = true,
        });
    }

    private Vector2 SnapRoadPoint(Vector2 center, string roadToken, float offsetMeters, float dirX, float dirZ)
    {
        foreach (var line in KristiansandMapFeatures.Visible)
        {
            if (!string.Equals(line.Kind, "road", StringComparison.Ordinal))
                continue;
            if (!line.Name.Contains(roadToken, StringComparison.OrdinalIgnoreCase))
                continue;

            var arc = line.NearestArc(center);
            var point = line.At(arc);
            var norm = MathF.Max(1f, MathF.Sqrt(dirX * dirX + dirZ * dirZ));
            return point + new Vector2(dirX / norm * offsetMeters, dirZ / norm * offsetMeters);
        }

        var fallback = center + new Vector2(dirX, dirZ) * offsetMeters;
        return fallback;
    }

    private static float LaneYaw(RouteLane lane, float arc)
    {
        var point = lane.Line.At(arc);
        var probe = lane.Line.At(Math.Clamp(arc - lane.Away * 20f, 0f, lane.Line.Length));
        var forward = probe - point;
        if (forward.LengthSquared() < 1f)
            return 0f;

        return MathF.Atan2(forward.Y, forward.X) * (180f / MathF.PI);
    }

    private static void AddOdderoyaSpots(float east, float north, List<SiteSpot> spots)
    {
        var island = KristiansandFort.ToWorldMeters();
        const int spokes = 12;
        const int rings = 5;
        for (var ring = 1; ring <= rings; ring++)
        {
            var radius = 120f + ring * 90f;
            for (var s = 0; s < spokes; s++)
            {
                var angle = s * (MathF.PI * 2f / spokes);
                var x = island.X + MathF.Cos(angle) * radius;
                var z = island.Y + MathF.Sin(angle) * radius;
                if (x < 40f || z < 40f || x > east - 40f || z > north - 40f)
                    continue;

                spots.Add(new SiteSpot(x, z, 8f, false));
            }
        }
    }

    private void SpawnOtherRoads()
    {
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        if (!TrySpawnPrimaryRoadColumn(215f, "Strai"))
            SpawnBearingColumn(east, north, 215f, "Strai");
        if (!TrySpawnPrimaryRoadColumn(255f, "Vågsbygd"))
            SpawnBearingColumn(east, north, 255f, "Vågsbygd");
        if (!TrySpawnPrimaryRoadColumn(125f, "Søm"))
            SpawnBearingColumn(east, north, 125f, "Søm");
    }

    private void SpawnBearingColumn(float east, float north, float approachBearingDegrees, string name)
    {
        var rad = approachBearingDegrees * (MathF.PI / 180f);
        // Bearing 0 = east (+X), 90 = north (+Z). Approach FROM that direction toward objective.
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

    private static bool BesideTheGun(GroundPolyline line, Vector2 gun)
    {
        var nearest = line.At(line.NearestArc(gun));
        return Vector2.Distance(nearest, gun) < 380f;
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

    private static float DeltaDegrees(float a, float b)
    {
        var delta = a - b;
        while (delta > 180f)
            delta -= 360f;
        while (delta < -180f)
            delta += 360f;
        return delta;
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
                InArc = MathF.Abs(DeltaDegrees(bearing, TrailBearingDegrees)) <= 22.5f,
                Posture = unit.Alive ? unit.Posture : "stopped",
                MetersFromGun = MathF.Sqrt(dx * dx + dz * dz),
                BearingDegrees = bearing,
                MetersToCrossing = Vector2.Distance(new Vector2(unit.X, unit.Z), new Vector2(Objective.X, Objective.Z)),
            };
        }

        return reports;
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

    private static Vector3 FindValley(TerrainWorld terrain, float east, float north)
    {
        var best = float.MaxValue;
        var at = new Vector3(east * 0.5f, 0f, north * 0.5f);
        const int n = 48;
        for (var j = 0; j < n; j++)
        {
            var z = north * (0.30f + 0.40f * j / (n - 1f));
            for (var i = 0; i < n; i++)
            {
                var x = east * (0.30f + 0.40f * i / (n - 1f));
                var h = terrain.Field.SampleHeight(x, z);
                if (h >= best)
                    continue;

                best = h;
                at = new Vector3(x, h, z);
            }
        }

        return at;
    }

    private readonly struct GunSite(Vector3 position, float openingElevationDegrees, float columnLeadMeters)
    {
        public Vector3 Position { get; } = position;
        public float OpeningElevationDegrees { get; } = openingElevationDegrees;
        public float ColumnLeadMeters { get; } = columnLeadMeters;
    }

    private readonly struct SiteSpot(float x, float z, float minAdvantage, bool alongRoad)
    {
        public float X { get; } = x;
        public float Z { get; } = z;
        public float MinAdvantage { get; } = minAdvantage;
        public bool AlongRoad { get; } = alongRoad;
    }

    private struct SiteCandidate
    {
        public Vector3 Gun;
        public float Bearing;
        public float ObjDist;
        public float Lead;
        public float Score;
    }

    /// <summary>
    /// Picks a trail position by integrating the library trajectory on this heightfield.
    /// The site has to be able to land a shell on the column. A shoulder that jumps from the near bank to the far valley is rejected.
    /// </summary>
    private GunSite FindGun(TerrainWorld terrain, float east, float north, Vector3 objective)
    {
        var atmosphere = AtmosphereModel.CreateRegionalDefault();
        var simulation = new ProjectileBallisticSimulation(GunModel.Educational155Profile, DragCoefficientLaw.BoatTail);
        var spots = new List<SiteSpot>(120);
        AddOdderoyaSpots(east, north, spots);

        var shortlist = new List<SiteCandidate>(16);
        var roads = new List<SiteCandidate>(40);
        ReadOnlySpan<float> elevations = [8f, 12f, 16f, 22f, 28f, 36f, 44f, 52f];
        Span<float> reaches = stackalloc float[elevations.Length];
        foreach (var spot in spots)
        {
            var h = terrain.Field.SampleHeight(spot.X, spot.Z);
            if (h - objective.Y < spot.MinAdvantage)
                continue;

            var gun = new Vector3(spot.X, h, spot.Z);
            var bearing = BearingDegrees(gun, objective);
            var objDist = Horizontal(gun, objective);
            if (objDist < 1_400f || objDist > 7_000f)
                continue;

            var maxReach = 0f;
            var minReach = float.MaxValue;
            var inBand = false;
            var bandNear = objDist + 200f;
            var bandFar = objDist + 1_700f;
            for (var i = 0; i < elevations.Length; i++)
            {
                reaches[i] = ReachMeters(terrain, atmosphere, simulation, gun, bearing, elevations[i]);
                maxReach = MathF.Max(maxReach, reaches[i]);
                minReach = MathF.Min(minReach, reaches[i]);
                if (reaches[i] >= bandNear && reaches[i] <= bandFar)
                    inBand = true;
            }

            if (maxReach < objDist + 400f || minReach > objDist + 2_200f)
                continue;

            var lead = Math.Clamp(maxReach - objDist - 200f, 700f, 1_800f);
            var cover = MathF.Min(maxReach - objDist, lead + SpacingMeters * (ColumnCount - 1));
            var score = cover + MathF.Min(h - objective.Y, 350f) * 0.04f - MathF.Abs(objDist - 3_200f) * 0.02f;
            if (!inBand)
                score -= 4_000f;

            var candidate = new SiteCandidate
            {
                Gun = gun,
                Bearing = bearing,
                ObjDist = objDist,
                Lead = lead,
                Score = score,
            };
            if (spot.AlongRoad)
                roads.Add(candidate);
            else
                Consider(shortlist, candidate);
        }

        var bestScore = float.NegativeInfinity;
        var best = new GunSite(
            new Vector3(Math.Clamp(objective.X - 2_500f, 200f, east - 200f), objective.Y, objective.Z),
            16f,
            1_200f);
        foreach (var candidate in roads.Concat(shortlist))
        {
            if (!TryHitColumnRoad(terrain, atmosphere, simulation, candidate, out var opening, out var leadArc))
                continue;

            if (candidate.Score <= bestScore)
                continue;

            bestScore = candidate.Score;
            best = new GunSite(candidate.Gun, opening, leadArc);
        }

        if (bestScore > float.NegativeInfinity)
            return best;

        var fallback = best.Position;
        return new GunSite(
            new Vector3(fallback.X, terrain.Field.SampleHeight(fallback.X, fallback.Z), fallback.Z),
            16f,
            1_200f);
    }

    private readonly struct OpeningLay(float elevation, int charge, float bearing)
    {
        public float Elevation { get; } = elevation;
        public int Charge { get; } = charge;
        public float Bearing { get; } = bearing;
    }

    private OpeningLay TuneOpening(TerrainWorld terrain, Vector3 gun, float fallbackElevation)
    {
        var desired = float.MaxValue;
        var aimBearing = TrailBearingDegrees;
        var nearestCrossing = float.MaxValue;
        foreach (var unit in _units)
        {
            if (!unit.Alive)
                continue;

            var bearing = MathF.Atan2(unit.Z - gun.Z, unit.X - gun.X) * (180f / MathF.PI);
            if (MathF.Abs(DeltaDegrees(bearing, TrailBearingDegrees)) > 22.5f)
                continue;

            var toCrossing = Horizontal(Objective, new Vector3(unit.X, Objective.Y, unit.Z));
            if (toCrossing >= nearestCrossing)
                continue;

            nearestCrossing = toCrossing;
            desired = Horizontal(gun, new Vector3(unit.X, gun.Y, unit.Z));
            aimBearing = bearing;
        }

        if (desired == float.MaxValue)
            desired = Horizontal(gun, Objective);
        var atmosphere = AtmosphereModel.CreateRegionalDefault();
        var simulation = new ProjectileBallisticSimulation(GunModel.Educational155Profile, DragCoefficientLaw.BoatTail);
        var bestElev = 62f;
        var bestCharge = 3;
        var bestScore = float.NegativeInfinity;
        var closestError = float.MaxValue;
        var closeElev = 70f;
        var closeCharge = 3;
        for (var pass = 0; pass < 2; pass++)
        {
            var charge = pass == 0 ? 3 : 0;
            var speed = SimulationUnits.ChargeSpeedsMps[charge];
            for (var elev = 71.5f; elev >= 48f; elev -= 1f)
            {
                var reach = ReachMeters(terrain, atmosphere, simulation, gun, aimBearing, elev, speed);
                if (reach < 600f)
                    continue;

                var error = MathF.Abs(reach - desired);
                if (error < closestError)
                {
                    closestError = error;
                    closeElev = elev;
                    closeCharge = charge;
                }

                var longBy = reach - desired;
                if (longBy > 160f || longBy < -120f)
                    continue;

                var score = elev - MathF.Abs(longBy) * 0.04f;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestElev = elev;
                bestCharge = charge;
            }
        }

        if (bestScore > float.NegativeInfinity)
            return new OpeningLay(bestElev, bestCharge, aimBearing);

        if (closestError == float.MaxValue)
            return new OpeningLay(MathF.Max(fallbackElevation, 55f), 3, aimBearing);

        return new OpeningLay(closeElev, closeCharge, aimBearing);
    }

    private static void Consider(List<SiteCandidate> shortlist, SiteCandidate candidate)
    {
        if (shortlist.Count < 16)
        {
            shortlist.Add(candidate);
            return;
        }

        var worst = 0;
        for (var i = 1; i < shortlist.Count; i++)
        {
            if (shortlist[i].Score < shortlist[worst].Score)
                worst = i;
        }

        if (candidate.Score > shortlist[worst].Score)
            shortlist[worst] = candidate;
    }

    private bool TryHitColumnRoad(
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        ProjectileBallisticSimulation simulation,
        SiteCandidate candidate,
        out float elevation,
        out float leadArc)
    {
        elevation = 16f;
        leadArc = 900f;
        if (_road is null)
            return false;

        var gunArc = _road.NearestArc(new Vector2(candidate.Gun.X, candidate.Gun.Z));
        var away = MathF.Abs(gunArc - _crossingArc) < 500f
            ? (_road.Length - _crossingArc >= _crossingArc ? 1 : -1)
            : (gunArc < _crossingArc ? 1 : -1);
        ReadOnlySpan<float> steps = [550f, 800f, 1_050f, 1_350f, 1_650f];
        var matched = false;
        foreach (var step in steps)
        {
            var point = _road.At(Math.Clamp(_crossingArc + away * step, 0f, _road.Length));
            var bearing = BearingDegrees(candidate.Gun, new Vector3(point.X, 0f, point.Y));
            if (MathF.Abs(DeltaDegrees(bearing, candidate.Bearing)) > 18f)
                break;

            var range = Horizontal(candidate.Gun, new Vector3(point.X, candidate.Gun.Y, point.Y));
            if (TryPlunging(terrain, atmosphere, simulation, candidate.Gun, bearing, range, out var steep, out _))
            {
                elevation = steep;
                leadArc = step;
                matched = true;
                continue;
            }

            if (matched)
                continue;

            float? previousReach = null;
            var previousElev = 0f;
            for (var elev = 2f; elev <= 32f; elev += 2f)
            {
                var reach = ReachMeters(terrain, atmosphere, simulation, candidate.Gun, bearing, elev);
                if (reach < 400f)
                {
                    previousReach = null;
                    continue;
                }

                if (MathF.Abs(reach - range) <= 220f
                    || (previousReach is float previous
                        && range >= MathF.Min(previous, reach)
                        && range <= MathF.Max(previous, reach)
                        && MathF.Abs(reach - previous) < 700f))
                {
                    elevation = previousReach is float prior && MathF.Abs(prior - range) < MathF.Abs(reach - range)
                        ? previousElev
                        : elev;
                    leadArc = step;
                    matched = true;
                    break;
                }

                previousReach = reach;
                previousElev = elev;
            }
        }

        return matched;
    }

    private static bool TryPlunging(
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        ProjectileBallisticSimulation simulation,
        Vector3 gun,
        float bearing,
        float range,
        out float elevation,
        out int charge)
    {
        elevation = 62f;
        charge = 0;
        var bestElev = -1f;
        var bestCharge = 0;
        for (var zone = 0; zone < 2; zone++)
        {
            var zoneCharge = zone == 0 ? 3 : 0;
            var speed = SimulationUnits.ChargeSpeedsMps[zoneCharge];
            for (var elev = 70f; elev >= 48f; elev -= 4f)
            {
                var reach = ReachMeters(terrain, atmosphere, simulation, gun, bearing, elev, speed);
                if (reach < 600f || MathF.Abs(reach - range) > 380f)
                    continue;
                if (elev <= bestElev)
                    continue;

                bestElev = elev;
                bestCharge = zoneCharge;
            }
        }

        if (bestElev < 0f)
            return false;

        elevation = bestElev;
        charge = bestCharge;
        return true;
    }

    private static void AddRingSpots(Vector3 objective, float east, float north, List<SiteSpot> spots)
    {
        const int spokes = 24;
        for (var r = 0; r < 8; r++)
        {
            var dist = 1_200f + r * 420f;
            for (var s = 0; s < spokes; s++)
            {
                var angle = s / (float)spokes * MathF.Tau;
                var x = objective.X + MathF.Cos(angle) * dist;
                var z = objective.Z + MathF.Sin(angle) * dist;
                if (x < 180f || z < 180f || x > east - 180f || z > north - 180f)
                    continue;

                spots.Add(new SiteSpot(x, z, 15f, false));
            }
        }
    }

    private static void AddRoadSpots(GroundPolyline road, Vector3 objective, float east, float north, List<SiteSpot> spots)
    {
        var arc = road.NearestArc(new Vector2(objective.X, objective.Z));
        var back = road.At(MathF.Max(0f, arc - 200f));
        var fore = road.At(MathF.Min(road.Length, arc + 200f));
        var tx = fore.X - back.X;
        var tz = fore.Y - back.Y;
        var length = MathF.Sqrt(tx * tx + tz * tz);
        if (length < 1f)
            return;

        tx /= length;
        tz /= length;
        var px = -tz;
        var pz = tx;
        ReadOnlySpan<float> alongs = [-3_600f, -2_400f, -1_500f, 1_500f, 2_400f, 3_600f];
        ReadOnlySpan<float> laterals = [160f, 420f, 780f];
        foreach (var along in alongs)
        {
            foreach (var lateral in laterals)
            {
                for (var side = -1; side <= 1; side += 2)
                {
                    var x = objective.X + tx * along + px * lateral * side;
                    var z = objective.Z + tz * along + pz * lateral * side;
                    if (x < 180f || z < 180f || x > east - 180f || z > north - 180f)
                        continue;

                    spots.Add(new SiteSpot(x, z, 0f, true));
                }
            }
        }
    }

    private static float ReachMeters(
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        ProjectileBallisticSimulation simulation,
        Vector3 gun,
        float azimuthDegrees,
        float elevationDegrees,
        float muzzleSpeedMps = 0f)
    {
        var elev = elevationDegrees * (MathF.PI / 180f);
        var az = azimuthDegrees * (MathF.PI / 180f);
        var cosE = MathF.Cos(elev);
        var direction = Vector3.Normalize(new Vector3(cosE * MathF.Cos(az), MathF.Sin(elev), cosE * MathF.Sin(az)));
        var pivot = gun + new Vector3(0f, SimulationUnits.GunHeightOffset, 0f);
        var muzzle = pivot + direction * GunModel.BarrelLength;
        var speed = muzzleSpeedMps > 1f ? muzzleSpeedMps : SimulationUnits.ChargeSpeedsMps[1];
        var state = new ProjectileState(muzzle, direction * speed, GunModel.Educational155Profile.MassKg, timeSeconds: 0);
        var environment = atmosphere.BallisticEnvironment(true, muzzle.Y);
        var preview = BallisticTrajectoryRunner.BuildPreview(
            simulation,
            environment,
            terrain.Field,
            state,
            dtSeconds: 1.0 / 60.0,
            maxTimeSeconds: 55,
            maxPoints: 4_000);
        return Horizontal(pivot, preview[^1]);
    }

    private static float Horizontal(Vector3 from, Vector3 to)
    {
        var dx = to.X - from.X;
        var dz = to.Z - from.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static float BearingDegrees(Vector3 from, Vector3 to)
    {
        var dx = to.X - from.X;
        var dz = to.Z - from.Z;
        return MathF.Atan2(dz, dx) * (180f / MathF.PI);
    }

    private static void DrawRing(SilkWireBatch wires, Vector3 center, float radius, Color color)
    {
        const int segments = 24;
        var previous = center + new Vector3(radius, 0f, 0f);
        for (var i = 1; i <= segments; i++)
        {
            var angle = i / (float)segments * MathF.Tau;
            var next = center + new Vector3(MathF.Cos(angle) * radius, 0f, MathF.Sin(angle) * radius);
            wires.Line(previous, next, color);
            previous = next;
        }
    }

    private struct ColumnUnit
    {
        public int Id;
        public int Lane;
        public float Arc;
        public float X;
        public float Z;
        public float Offset;
        public float Hold;
        public float Scare;
        public bool Passing;
        public bool Alive;
        public string Posture;
    }

    private struct RouteLane
    {
        public GroundPolyline Line;
        public float CrossingArc;
        public int Away;
        public string Name;
    }

    private struct DefenderUnit
    {
        public string Label;
        public float X;
        public float Z;
        public float YawDegrees;
        public bool Alive;
    }

    private struct PendingWave
    {
        public PendingWave(string roadToken, float atSeconds, bool spawned)
        {
            RoadToken = roadToken;
            AtSeconds = atSeconds;
            Spawned = spawned;
        }

        public string RoadToken;
        public float AtSeconds;
        public bool Spawned;
    }
}
