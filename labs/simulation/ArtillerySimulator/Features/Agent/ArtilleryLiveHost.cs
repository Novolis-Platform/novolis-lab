using System.Globalization;
using Novolis.Agent.Core;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>Agent host for the windowed range. Commands run on the frame thread; callers wait for the result.</summary>
internal sealed class ArtilleryLiveHost : IArtillerySurface
{
    private const string GunResource = "artillery.gun";

    private readonly ArtilleryApp _game;
    private readonly object _gate = new();
    private readonly Queue<Pending> _queue = new();
    private readonly Dictionary<string, AgentCommandResult> _completed = new(StringComparer.Ordinal);
    private Pending? _shot;
    private long _revision = 1;
    private long _sequence;
    private string _seen = "";
    private AgentReceipt? _receipt;
    private AgentLastAction? _last;

    public ArtilleryLiveHost(ArtilleryApp game)
    {
        _game = game;
    }

#pragma warning disable CS0067
    public event Action<AgentDecisionEvent>? Decision;
#pragma warning restore CS0067

    public event Action<AgentChangedEvent>? Changed;

    public event Action<AgentActionResultEvent>? ActionResult;

    public void RunFrame(Action step)
    {
        List<Pending> finished = [];
        AgentChangedEvent? changed = null;
        lock (_gate)
        {
            Drain(finished);
            step();
            CompleteShot(finished);
            changed = NoteChange();
        }

        foreach (var item in finished)
        {
            if (item.Result is { } result)
                item.Done.TrySetResult(result);
        }

        if (changed is not null)
            Changed?.Invoke(changed);
    }

    public AgentHello Hello()
    {
        lock (_gate)
        {
            return new AgentHello
            {
                Sequence = ++_sequence,
                ProtocolVersion = "1.0",
                AppId = "artillery-simulator",
                AppTitle = "Artillery Simulator",
                ProcessId = Environment.ProcessId,
                SurfaceId = "artillery",
                Description = "The Defense of Kristiansand. Protect downtown defenders from staggered columns. Order the lay and fire when the crew has the breech closed.",
                HttpPort = 18770,
                TcpPort = 18771,
                Capabilities =
                [
                    "artillery.aim",
                    "artillery.charge",
                    "artillery.fire",
                    "artillery.pace",
                    "artillery.camera",
                    AgentMethodNames.Watch,
                    AgentMethodNames.WatchNext,
                    AgentMethodNames.Unwatch,
                ],
            };
        }
    }

    public AgentSnapshot Snapshot()
    {
        lock (_gate)
            return Capture();
    }

    public AgentActionsResponse Actions()
    {
        lock (_gate)
            return new AgentActionsResponse { Sequence = ++_sequence, Actions = Catalog() };
    }

    public AgentCommandResult Execute(AgentCommand command)
    {
        TaskCompletionSource<AgentCommandResult> done;
        lock (_gate)
        {
            if (!string.IsNullOrEmpty(command.CorrelationId) && _completed.TryGetValue(command.CorrelationId, out var prior))
                return prior;

            if (_shot is not null
                && !string.IsNullOrEmpty(command.CorrelationId)
                && command.CorrelationId == _shot.Command.CorrelationId)
            {
                done = _shot.Done;
            }
            else
            {
                done = new TaskCompletionSource<AgentCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                _queue.Enqueue(new Pending(command, done));
            }
        }

        if (!done.Task.Wait(TimeSpan.FromSeconds(120)))
        {
            return new AgentCommandResult
            {
                Ok = false,
                ActionId = command.ActionId,
                CorrelationId = command.CorrelationId,
                ErrorCode = "timeout",
                Message = "The range did not finish the command within 120 s.",
            };
        }

        return done.Task.Result;
    }

    public AgentCommandResult Continue() =>
        new() { Ok = true, Message = "The range has no decision gate.", StateRevision = _revision };

    public void Subscribe()
    {
    }

    private void Drain(List<Pending> finished)
    {
        while (_shot is null && _queue.Count > 0)
        {
            var item = _queue.Dequeue();
            item.Result = Apply(item.Command);
            if (item.Result is null)
            {
                _shot = item;
                continue;
            }

            Remember(item);
            finished.Add(item);
        }
    }

    private void CompleteShot(List<Pending> finished)
    {
        if (_shot is null || _game.Shot.Phase == BallisticTrajectoryPhase.InFlight)
            return;

        var item = _shot;
        _shot = null;
        item.Result = FinishFire(item.Command);
        Remember(item);
        finished.Add(item);
    }

    private AgentCommandResult? Apply(AgentCommand command)
    {
        var action = Catalog().FirstOrDefault(candidate => candidate.Id == command.ActionId);
        var rejected = AgentCommandGate.Reject(action, command, _revision);
        if (rejected is not null)
            return rejected;

        if (action is not { Enabled: true })
            return Reject(command, "unavailable", action?.DisabledReason ?? "Unknown action.");

        switch (command.ActionId)
        {
            case "artillery.aim":
                if (!TryNumber(command, "elevation", out var elevation) || !TryNumber(command, "azimuth", out var azimuth))
                    return Reject(command, "invalid-arguments", "Aim needs elevation and azimuth.");
                _game.OrderAim((float)elevation, (float)azimuth);
                var gun = _game.Gun;
                var crew = _game.Crew;
                var message = crew.Ready(gun)
                    ? string.Create(CultureInfo.InvariantCulture, $"On lay at {crew.OrderedElevation:F1}° elevation, {crew.OrderedAzimuth:F1}° azimuth.")
                    : string.Create(CultureInfo.InvariantCulture, $"Laying to {crew.OrderedElevation:F1}° / {crew.OrderedAzimuth:F1}°. Tube at {gun.ElevationDegrees:F1}° / {gun.AzimuthDegrees:F1}°.");
                return Accept(command, message);
            case "artillery.charge":
                if (!TryNumber(command, "index", out var index))
                    return Reject(command, "invalid-arguments", "Charge needs an index.");
                _game.OrderCharge((int)index);
                var seated = _game.Crew.Ready(_game.Gun)
                    ? string.Create(CultureInfo.InvariantCulture, $"Charge {_game.Gun.ChargeLabel} ({_game.Gun.MuzzleSpeedMps:F0} m/s).")
                    : string.Create(CultureInfo.InvariantCulture, $"Charge {_game.Gun.ChargeLabel} ({_game.Gun.MuzzleSpeedMps:F0} m/s). {_game.Crew.Phrase(_game.Gun)}");
                return Accept(command, seated);
            case "artillery.pace":
                if (!TryNumber(command, "index", out var pace))
                    return Reject(command, "invalid-arguments", "Pace needs an index.");
                _game.SetPace((int)pace);
                return Accept(command, _game.PaceText);
            case "artillery.camera":
                if (!command.Params.TryGetValue("mode", out var mode))
                    return Reject(command, "invalid-arguments", "Camera needs mode shell or gun.");
                var followShell = mode.Equals("shell", StringComparison.OrdinalIgnoreCase)
                    || mode.Equals("follow", StringComparison.OrdinalIgnoreCase)
                    || mode.Equals("projectile", StringComparison.OrdinalIgnoreCase);
                _game.SetFollowCamera(followShell);
                return Accept(command, followShell ? "Camera follows the shell." : "Camera follows the gun.");
            case "artillery.fire":
                if (_game.FireBlockReason() is { } blocked)
                    return Reject(command, "unavailable", blocked);
                _game.TryFire();
                return null;
            default:
                return Reject(command, "unavailable", "Unknown action.");
        }
    }

    private AgentCommandResult FinishFire(AgentCommand command)
    {
        var impact = _game.Shot.Impact;
        var message = impact is { } hit
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Impact {hit.HorizontalRangeMeters / 1000f:F2} km after {hit.TimeSeconds:F1} s ({hit.Reason}), {hit.ImpactSpeedMps:F0} m/s. {_game.Mission?.ResultPhrase()}")
            : "The shot did not impact.";
        return Accept(command, message);
    }

    private AgentCommandResult Accept(AgentCommand command, string message)
    {
        _revision++;
        _receipt = AgentCommandGate.CreateReceipt(command, AgentLifecycle.Applied, true, message, _revision);
        _last = new AgentLastAction { ActionId = command.ActionId, Ok = true, Message = message };
        var snapshot = Capture();
        var result = new AgentCommandResult
        {
            Sequence = ++_sequence,
            Ok = true,
            ActionId = command.ActionId,
            CorrelationId = command.CorrelationId,
            Message = message,
            StateRevision = _revision,
            Receipt = _receipt,
            Snapshot = snapshot,
        };
        ActionResult?.Invoke(new AgentActionResultEvent
        {
            Sequence = _sequence,
            ActionId = command.ActionId,
            Ok = true,
            Message = message,
            CorrelationId = command.CorrelationId,
            ResourceId = GunResource,
            Severity = AgentSeverity.Info,
            Receipt = _receipt,
            Snapshot = snapshot,
        });
        return result;
    }

    private AgentChangedEvent? NoteChange()
    {
        var gun = _game.Gun;
        var shot = _game.Shot;
        var mission = _game.Mission;
        var crew = _game.Crew;
        var defense = mission is null
            ? ""
            : string.Create(
                CultureInfo.InvariantCulture,
                $"|{mission.Remaining}|{(int)(mission.NearestMeters / 25f)}|{mission.ObjectiveLost}|{mission.Held}|{mission.PostureToken()}|{crew.Token(gun)}|{(int)MathF.Ceiling(crew.BusySeconds(gun))}");
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{shot.Phase}|{gun.ElevationDegrees:F0}|{gun.AzimuthDegrees:F0}|{crew.OrderedElevation:F0}|{crew.OrderedAzimuth:F0}|{crew.OrderedCharge}|{_game.CameraMode}|{_game.PaceText}|{shot.Impact?.HorizontalRangeMeters:F0}{defense}");
        if (key == _seen)
            return null;

        _seen = key;
        return new AgentChangedEvent
        {
            Sequence = ++_sequence,
            Reason = shot.Phase.ToString(),
            ResourceId = GunResource,
            Severity = AgentSeverity.Info,
            Snapshot = Capture(),
        };
    }

    private void Remember(Pending item)
    {
        if (item.Result is { } result && !string.IsNullOrEmpty(item.Command.CorrelationId) && result.Ok)
            _completed[item.Command.CorrelationId] = result;
    }

    private AgentCommandResult Reject(AgentCommand command, string reason, string message)
    {
        var receipt = AgentCommandGate.CreateReceipt(command, AgentLifecycle.Rejected, false, message, _revision, reason, message);
        return new AgentCommandResult
        {
            Sequence = ++_sequence,
            Ok = false,
            ActionId = command.ActionId,
            CorrelationId = command.CorrelationId,
            ErrorCode = reason,
            Message = message,
            StateRevision = _revision,
            Receipt = receipt,
        };
    }

    private AgentSnapshot Capture()
    {
        var gun = _game.Gun;
        var shot = _game.Shot;
        var terrain = _game.Terrain;
        var impact = shot.Impact;
        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["elevation"] = gun.ElevationDegrees.ToString("F1", CultureInfo.InvariantCulture),
            ["azimuth"] = gun.AzimuthDegrees.ToString("F1", CultureInfo.InvariantCulture),
            ["charge"] = gun.ChargeLabel,
            ["muzzleMps"] = gun.MuzzleSpeedMps.ToString("F0", CultureInfo.InvariantCulture),
            ["phase"] = shot.Phase.ToString(),
            ["camera"] = _game.CameraMode.ToString(),
            ["pace"] = _game.PaceText,
            ["orderedElevation"] = _game.Crew.OrderedElevation.ToString("F1", CultureInfo.InvariantCulture),
            ["orderedAzimuth"] = _game.Crew.OrderedAzimuth.ToString("F1", CultureInfo.InvariantCulture),
            ["crew"] = _game.Crew.Token(gun),
            ["reload"] = _game.Crew.Ready(gun) ? "ready" : MathF.Ceiling(_game.Crew.BusySeconds(gun)).ToString("F0", CultureInfo.InvariantCulture),
            ["predictedMeters"] = _game.PredictedRangeMeters.ToString("F0", CultureInfo.InvariantCulture),
            ["terrain"] = "Kristiansand",
            ["latitude"] = KristiansandHeightSampler.MeanLatitudeDegrees.ToString("F2", CultureInfo.InvariantCulture),
        };
        if (_game.Mission is { } mission)
        {
            lines["deflection"] = gun.DeflectionDegrees.ToString("F1", CultureInfo.InvariantCulture);
            lines["trail"] = gun.CenterAzimuthDegrees.ToString("F1", CultureInfo.InvariantCulture);
            lines["objectiveX"] = mission.Objective.X.ToString("F0", CultureInfo.InvariantCulture);
            lines["objectiveZ"] = mission.Objective.Z.ToString("F0", CultureInfo.InvariantCulture);
            lines["enemies"] = mission.Remaining.ToString(CultureInfo.InvariantCulture);
            lines["nearestMeters"] = mission.NearestMeters.ToString("F0", CultureInfo.InvariantCulture);
            lines["defenders"] = mission.DefendersAlive.ToString(CultureInfo.InvariantCulture);
            lines["fratricide"] = mission.FratricideLoss ? "yes" : "no";
            lines["defense"] = mission.FratricideLoss || mission.ObjectiveLost ? "lost" : mission.Held ? "held" : "open";
        }
        if (impact is { } hit)
        {
            lines["rangeMeters"] = hit.HorizontalRangeMeters.ToString("F0", CultureInfo.InvariantCulture);
            lines["flightSeconds"] = hit.TimeSeconds.ToString("F1", CultureInfo.InvariantCulture);
            lines["impact"] = hit.Reason.ToString();
        }

        var defense = _game.Mission?.Observation();
        var crewNote = _game.Crew.Ready(gun) ? "" : " " + _game.Crew.Phrase(gun);
        var summary = impact is { } recorded
            ? string.Create(CultureInfo.InvariantCulture, $"{shot.Phase}: aim {gun.ElevationDegrees:F0}° {gun.ChargeLabel}, impact {recorded.HorizontalRangeMeters / 1000f:F2} km ({recorded.Reason}). {defense}{crewNote}")
            : string.Create(CultureInfo.InvariantCulture, $"{shot.Phase}: aim {_game.Crew.OrderedElevation:F0}° / {_game.Crew.OrderedAzimuth:F0}°, {gun.ChargeLabel}, predicted {_game.PredictedRangeMeters / 1000f:F2} km. {defense}{crewNote}");

        return new AgentSnapshot
        {
            Sequence = ++_sequence,
            HubId = GunResource,
            HubName = "155 mm gun",
            DocumentName = "The Defense of Kristiansand",
            SeedHash = "42",
            PauseReason = shot.Phase.ToString(),
            StateRevision = _revision,
            MetadataVersion = AgentMetadata.Version,
            StatusLines = lines,
            MapVisible = true,
            MapX = impact?.Position.X ?? terrain.GunBaseline.X,
            MapY = impact?.Position.Z ?? terrain.GunBaseline.Z,
            Actions = Catalog(),
            Receipt = _receipt,
            LastAction = _last,
            Observations =
            [
                new AgentObservation
                {
                    ResourceId = GunResource,
                    StateRevision = _revision,
                    Summary = summary,
                    AvailableActionIds = Catalog().Where(action => action.Enabled).Select(action => action.Id).ToArray(),
                },
            ],
            Entities = BuildEntities(),
        };
    }

    private AgentEntity[] BuildEntities()
    {
        if (_game.Mission is not { } mission)
            return [];

        var gun = _game.Terrain.GunBaseline;
        var reports = mission.Reports(gun);
        var entities = new List<AgentEntity>(reports.Length + 12);
        foreach (var threat in reports)
        {
            var where = threat.InArc ? "in traverse" : "outside traverse";
            entities.Add(new AgentEntity
            {
                Id = string.Create(CultureInfo.InvariantCulture, $"threat-{threat.Id}"),
                Kind = AgentEntity.Threat,
                Name = threat.Road,
                State = threat.Alive ? threat.Posture : "stopped",
                X = threat.X,
                Z = threat.Z,
                Summary = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{threat.Posture}, {where} on {threat.Road}, {threat.MetersFromGun / 1000f:F2} km from the gun at {threat.BearingDegrees:F0}°, {threat.MetersToCrossing / 1000f:F2} km from the crossing"),
            });
        }

        entities.Add(new AgentEntity
        {
            Id = "objective",
            Kind = AgentEntity.Objective,
            Name = "Kvadraturen",
            State = mission.ObjectiveLost ? "lost" : mission.Held ? "held" : "open",
            X = mission.Objective.X,
            Z = mission.Objective.Z,
            Summary = mission.Observation(),
        });

        foreach (var defender in mission.DefenderReports())
        {
            entities.Add(new AgentEntity
            {
                Id = string.Create(CultureInfo.InvariantCulture, $"defender-{defender.Label}"),
                Kind = "defender",
                Name = defender.Label,
                State = defender.Alive ? "holding" : "casualty",
                X = defender.X,
                Z = defender.Z,
                Summary = defender.Alive
                    ? "Friendly defender on a downtown chokepoint."
                    : "Defender out of action.",
            });
        }

        entities.AddRange(KristiansandMapFeatures.MapEntities());

        return entities.ToArray();
    }

    private AgentAction[] Catalog()
    {
        var blocked = _game.FireBlockReason();
        return
        [
            Action("artillery.aim", "Aim", "Order elevation and world azimuth. The crew lays the tube on the handwheels. Trails clamp the azimuth.", AgentActionSchema.Object(["elevation", "azimuth"], ("elevation", "number"), ("azimuth", "number")), true, null),
            Action("artillery.charge", "Charge", "Order reduced, standard, max, or low (0, 1, 2, or 3). A new zone is reseated before the next shot.", AgentActionSchema.Object(["index"], ("index", "integer")), true, null),
            Action("artillery.fire", "Fire", "Shoot when the breech is closed and the tube is on the order. The burst puts nearby vehicles out of action and pins the next ones.", null, blocked is null, blocked),
            Action("artillery.pace", "Pace", "Set pace 0..3. The shell, the column, and the crew share this clock.", AgentActionSchema.Object(["index"], ("index", "integer")), true, null),
            Action("artillery.camera", "Camera", "Choose follow or free.", AgentActionSchema.Object(["mode"], ("mode", "string")), true, null),
        ];
    }

    private static AgentAction Action(string id, string label, string summary, Dictionary<string, object?>? schema, bool enabled, string? disabledReason) =>
        new()
        {
            Id = id,
            Label = label,
            Summary = summary,
            Schema = schema,
            Enabled = enabled,
            DisabledReason = disabledReason,
            Capability = "artillery.play",
            SafetyClass = AgentSafetyClass.Mutate,
            ApprovalRequired = false,
            Idempotency = "correlation",
            MetadataVersion = AgentMetadata.Version,
        };

    private static bool TryNumber(AgentCommand command, string name, out double value)
    {
        value = 0;
        return command.Params.TryGetValue(name, out var text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryBool(AgentCommand command, string name, out bool value)
    {
        value = false;
        return command.Params.TryGetValue(name, out var text) && bool.TryParse(text, out value);
    }

    private sealed class Pending(AgentCommand command, TaskCompletionSource<AgentCommandResult> done)
    {
        public AgentCommand Command { get; } = command;
        public TaskCompletionSource<AgentCommandResult> Done { get; } = done;
        public AgentCommandResult? Result { get; set; }
    }
}
