using System.Globalization;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Novolis.Agent.Core;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>
/// Agent adapter: HTTP/MCP → <see cref="ChannelWriter{BattleCommand}"/> → frame pump.
/// Reads state only through <see cref="IPlayQuery"/>. No MediatR; no direct BattleWorld mutation.
/// </summary>
internal sealed class ArtilleryLiveHost : IArtillerySurface
{
    private const string GunResource = "artillery.gun";
    private const int MaxCompleted = 256;

    private readonly IPlayQuery _play;
    private readonly ChannelWriter<BattleCommand> _commands;
    private readonly ILogger<ArtilleryLiveHost> _log;
    private readonly object _gate = new();
    private readonly Dictionary<string, AgentCommandResult> _completed = new(StringComparer.Ordinal);
    private readonly Queue<string> _completedOrder = new();
    private readonly List<TrackedImpact> _awaitingImpact = [];
    private long _revision = 1;
    private long _sequence;
    private string _seen = "";
    private AgentReceipt? _receipt;
    private AgentLastAction? _last;

    public ArtilleryLiveHost(
        IPlayQuery play,
        ChannelWriter<BattleCommand> commands,
        ILogger<ArtilleryLiveHost>? log = null)
    {
        _play = play;
        _commands = commands;
        _log = log ?? NullLogger<ArtilleryLiveHost>.Instance;
    }

#pragma warning disable CS0067
    public event Action<AgentDecisionEvent>? Decision;
#pragma warning restore CS0067

    public event Action<AgentChangedEvent>? Changed;

    public event Action<AgentActionResultEvent>? ActionResult;

    public void RunFrame(Action step)
    {
        // Do not hold _gate across step(): BattleCommandPump completes agent TCSs during Drain,
        // and Execute waits on those TCSs then needs _gate for Accept — lock-through-step deadlocks.
        step();

        List<AgentActionResultEvent> impactEvents = [];
        AgentChangedEvent? changed;
        lock (_gate)
        {
            CollectImpacts(impactEvents);
            changed = NoteChange();
        }

        foreach (var evt in impactEvents)
            ActionResult?.Invoke(evt);

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
                AppTitle = ProductIdentity.Title,
                ProcessId = Environment.ProcessId,
                SurfaceId = "artillery",
                Description = $"{ProductIdentity.Title}: {ProductIdentity.Subtitle}. Level: {_play.Level.DisplayName}. Educational sandbox — {ProductLegal.Tagline}",
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
        lock (_gate)
        {
            if (!string.IsNullOrEmpty(command.CorrelationId))
            {
                if (_completed.TryGetValue(command.CorrelationId, out var prior))
                    return prior;
            }
        }

        if (command.ActionId is "artillery.pace")
        {
            lock (_gate)
                return Accept(command, "Realtime only — fixed battle clock.");
        }

        if (!TryBuildBattleCommand(command, out var battleCmd, out var reject))
            return reject!;

        var battleDone = battleCmd.Completion
            ?? throw new InvalidOperationException("Battle command missing completion source.");

        if (!_commands.TryWrite(battleCmd))
        {
            // Bounded Wait mode rarely fails TryWrite; fall back to async write wait.
            if (!_commands.WaitToWriteAsync().AsTask().Wait(TimeSpan.FromSeconds(30))
                || !_commands.TryWrite(battleCmd))
            {
                return new AgentCommandResult
                {
                    Ok = false,
                    ActionId = command.ActionId,
                    CorrelationId = command.CorrelationId,
                    ErrorCode = "timeout",
                    Message = "Battle command channel did not accept the write within 30 s.",
                };
            }
        }

        if (!battleDone.Task.Wait(TimeSpan.FromSeconds(30)))
        {
            return new AgentCommandResult
            {
                Ok = false,
                ActionId = command.ActionId,
                CorrelationId = command.CorrelationId,
                ErrorCode = "timeout",
                Message = "The range did not accept the command within 30 s.",
            };
        }

        var battle = battleDone.Task.Result;
        lock (_gate)
        {
            if (!battle.Succeeded)
                return Reject(command, battle.ErrorCode ?? "unavailable", battle.Message);

            if (command.ActionId is "artillery.fire" && battle.FiredRound is { } round)
                _awaitingImpact.Add(new TrackedImpact(round, command.CorrelationId));

            var result = Accept(command, battle.Message);
            _log.LogDebug(
                "Agent {ActionId} ok={Ok} corr={CorrelationId}",
                command.ActionId,
                result.Ok,
                command.CorrelationId);
            return result;
        }
    }

    public AgentCommandResult Continue() =>
        new() { Ok = true, Message = "The range has no decision gate.", StateRevision = _revision };

    public void Subscribe()
    {
    }

    private bool TryBuildBattleCommand(
        AgentCommand command,
        out BattleCommand battleCommand,
        out AgentCommandResult? reject)
    {
        battleCommand = null!;
        reject = null;
        var done = new TaskCompletionSource<BattleCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        switch (command.ActionId)
        {
            case "artillery.aim":
                if (!TryNumber(command, "elevation", out var elevation)
                    || !TryNumber(command, "azimuth", out var azimuth)
                    || !IsFinite(elevation)
                    || !IsFinite(azimuth))
                {
                    reject = Reject(command, "invalid-arguments", "Aim needs finite elevation and azimuth.");
                    return false;
                }

                battleCommand = BattleCommand.Aim((float)elevation, (float)azimuth, command.CorrelationId, done);
                return true;

            case "artillery.charge":
                if (!TryNumber(command, "index", out var index) || !IsFinite(index))
                {
                    reject = Reject(command, "invalid-arguments", "Charge needs a finite index.");
                    return false;
                }

                battleCommand = BattleCommand.Charge((int)Math.Round(index), command.CorrelationId, done);
                return true;

            case "artillery.camera":
                if (!command.Params.TryGetValue("mode", out var mode))
                {
                    reject = Reject(command, "invalid-arguments", "Camera needs mode shell|follow or gun.");
                    return false;
                }

                var followShell = mode.Equals("shell", StringComparison.OrdinalIgnoreCase)
                    || mode.Equals("follow", StringComparison.OrdinalIgnoreCase)
                    || mode.Equals("projectile", StringComparison.OrdinalIgnoreCase);
                battleCommand = BattleCommand.FollowCamera(followShell, command.CorrelationId, done);
                return true;

            case "artillery.fire":
                battleCommand = BattleCommand.Fire(command.CorrelationId, done);
                return true;

            default:
                reject = Reject(command, "unavailable", "Unknown action.");
                return false;
        }
    }

    private void CollectImpacts(List<AgentActionResultEvent> events)
    {
        for (var i = _awaitingImpact.Count - 1; i >= 0; i--)
        {
            var tracked = _awaitingImpact[i];
            if (tracked.Round.Phase == BallisticTrajectoryPhase.InFlight)
                continue;

            _awaitingImpact.RemoveAt(i);
            var impact = tracked.Round.Impact;
            var message = impact is { } hit
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"Impact {hit.HorizontalRangeMeters / 1000f:F2} km after {hit.TimeSeconds:F1} s ({hit.Reason}), {hit.ImpactSpeedMps:F0} m/s. {_play.Mission?.ResultPhrase()}")
                : "The shot did not impact.";

            _revision++;
            var snapshot = Capture();
            events.Add(new AgentActionResultEvent
            {
                Sequence = ++_sequence,
                ActionId = "artillery.impact",
                Ok = impact is not null,
                Message = message,
                CorrelationId = tracked.CorrelationId,
                ResourceId = GunResource,
                Severity = AgentSeverity.Info,
                Snapshot = snapshot,
            });
        }
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
        Remember(command.CorrelationId, result);
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
        var gun = _play.Gun;
        var shot = _play.Shot;
        var mission = _play.Mission;
        var crew = _play.Crew;
        var defense = mission is null
            ? ""
            : string.Create(
                CultureInfo.InvariantCulture,
                $"|{mission.Remaining}|{(int)(mission.NearestMeters / 25f)}|{mission.ObjectiveLost}|{mission.Held}|{mission.PostureToken()}|{crew.Token(gun)}|{(int)MathF.Ceiling(crew.BusySeconds(gun))}");
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{shot.Phase}|{gun.ElevationDegrees:F0}|{gun.AzimuthDegrees:F0}|{crew.OrderedElevation:F0}|{crew.OrderedAzimuth:F0}|{crew.OrderedCharge}|{_play.CameraModeName}|{_play.PaceText}|{shot.Impact?.HorizontalRangeMeters:F0}{defense}");
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

    private void Remember(string? correlationId, AgentCommandResult result)
    {
        if (string.IsNullOrEmpty(correlationId) || !result.Ok)
            return;

        _completed[correlationId] = result;
        _completedOrder.Enqueue(correlationId);
        while (_completedOrder.Count > MaxCompleted)
        {
            var old = _completedOrder.Dequeue();
            _completed.Remove(old);
        }
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
        var gun = _play.Gun;
        var shot = _play.Shot;
        var terrain = _play.Terrain;
        var impact = shot.Impact;
        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["elevation"] = gun.ElevationDegrees.ToString("F1", CultureInfo.InvariantCulture),
            ["azimuth"] = gun.AzimuthDegrees.ToString("F1", CultureInfo.InvariantCulture),
            ["charge"] = gun.ChargeLabel,
            ["muzzleMps"] = gun.MuzzleSpeedMps.ToString("F0", CultureInfo.InvariantCulture),
            ["phase"] = shot.Phase.ToString(),
            ["camera"] = _play.CameraModeName,
            ["pace"] = _play.PaceText,
            ["orderedElevation"] = _play.Crew.OrderedElevation.ToString("F1", CultureInfo.InvariantCulture),
            ["orderedAzimuth"] = _play.Crew.OrderedAzimuth.ToString("F1", CultureInfo.InvariantCulture),
            ["crew"] = _play.Crew.Token(gun),
            ["reload"] = _play.Crew.Ready(gun) ? "ready" : MathF.Ceiling(_play.Crew.BusySeconds(gun)).ToString("F0", CultureInfo.InvariantCulture),
            ["predictedMeters"] = _play.PredictedRangeMeters.ToString("F0", CultureInfo.InvariantCulture),
            ["gunX"] = terrain.GunBaseline.X.ToString("F0", CultureInfo.InvariantCulture),
            ["gunZ"] = terrain.GunBaseline.Z.ToString("F0", CultureInfo.InvariantCulture),
            ["terrain"] = "Kristiansand",
            ["latitude"] = KristiansandHeightSampler.MeanLatitudeDegrees.ToString("F2", CultureInfo.InvariantCulture),
            ["windMps"] = _play.Atmosphere.WindSpeedMetersPerSecond.ToString("F1", CultureInfo.InvariantCulture),
            ["windFrom"] = _play.Atmosphere.WindFromDegrees.ToString("F0", CultureInfo.InvariantCulture),
            ["windToward"] = _play.Atmosphere.WindTowardDegrees.ToString("F0", CultureInfo.InvariantCulture),
            ["tempC"] = (_play.Atmosphere.ReferenceTemperatureKelvin - 273.15).ToString("F1", CultureInfo.InvariantCulture),
            ["rh"] = (_play.Atmosphere.RelativeHumidity * 100.0).ToString("F0", CultureInfo.InvariantCulture),
            ["qnh"] = (_play.Atmosphere.ReferencePressurePa / 100.0).ToString("F0", CultureInfo.InvariantCulture),
            ["metAge"] = _play.Atmosphere.BattleAgeSeconds.ToString("F0", CultureInfo.InvariantCulture),
            ["level"] = _play.Level.Id,
            ["levelName"] = _play.Level.DisplayName,
            ["levelSite"] = _play.Level.SiteName,
            ["piece"] = gun.Spec.Id,
            ["pieceName"] = gun.Spec.ShortName,
            ["peElevMils"] = gun.Spec.Errors.ElevationPeMils.ToString("F2", CultureInfo.InvariantCulture),
            ["peDeflMils"] = gun.Spec.Errors.DeflectionPeMils.ToString("F2", CultureInfo.InvariantCulture),
            ["peMvFrac"] = gun.Spec.Errors.MuzzleVelocityPeFraction.ToString("F4", CultureInfo.InvariantCulture),
            ["heCaughtM"] = gun.Spec.Warhead.CaughtMeters.ToString("F0", CultureInfo.InvariantCulture),
            ["hePinnedM"] = gun.Spec.Warhead.PinnedMeters.ToString("F0", CultureInfo.InvariantCulture),
            ["heAirburstM"] = gun.Spec.Warhead.AirburstHeightMeters.ToString("F0", CultureInfo.InvariantCulture),
            ["seed"] = _play.Session.SeedHash,
        };
        if (_play.Delivery.LastFire is { } last)
        {
            lines["fireElevResMils"] = last.ElevationResidualMils.ToString("F2", CultureInfo.InvariantCulture);
            lines["fireDeflResMils"] = last.DeflectionResidualMils.ToString("F2", CultureInfo.InvariantCulture);
            lines["fireMvResFrac"] = last.MuzzleResidualFraction.ToString("F4", CultureInfo.InvariantCulture);
        }
        if (_play.Mission is { } mission)
        {
            lines["deflection"] = gun.DeflectionDegrees.ToString("F1", CultureInfo.InvariantCulture);
            lines["trail"] = gun.CenterAzimuthDegrees.ToString("F1", CultureInfo.InvariantCulture);
            lines["objectiveX"] = mission.Objective.X.ToString("F0", CultureInfo.InvariantCulture);
            lines["objectiveZ"] = mission.Objective.Z.ToString("F0", CultureInfo.InvariantCulture);
            lines["enemies"] = mission.Remaining.ToString(CultureInfo.InvariantCulture);
            lines["nearestMeters"] = mission.NearestMeters.ToString("F0", CultureInfo.InvariantCulture);
            lines["defenders"] = mission.DefendersAlive.ToString(CultureInfo.InvariantCulture);
            lines["fratricide"] = mission.FratricideLoss ? "yes" : "no";
            lines["outcome"] = mission.Outcome.ToString();
            lines["defense"] = mission.Outcome switch
            {
                MissionOutcome.Held => "held",
                MissionOutcome.Fratricide or MissionOutcome.ObjectiveLost => "lost",
                _ => "open",
            };
        }
        if (impact is { } hit)
        {
            lines["rangeMeters"] = hit.HorizontalRangeMeters.ToString("F0", CultureInfo.InvariantCulture);
            lines["flightSeconds"] = hit.TimeSeconds.ToString("F1", CultureInfo.InvariantCulture);
            lines["impact"] = hit.Reason.ToString();
        }

        var defense = _play.Mission?.Observation();
        var crewNote = _play.Crew.Ready(gun) ? "" : " " + _play.Crew.Phrase(gun);
        var summary = impact is { } recorded
            ? string.Create(CultureInfo.InvariantCulture, $"{shot.Phase}: aim {gun.ElevationDegrees:F0}° {gun.ChargeLabel}, impact {recorded.HorizontalRangeMeters / 1000f:F2} km ({recorded.Reason}). {defense}{crewNote}")
            : string.Create(CultureInfo.InvariantCulture, $"{shot.Phase}: aim {_play.Crew.OrderedElevation:F0}° / {_play.Crew.OrderedAzimuth:F0}°, {gun.ChargeLabel}, predicted {_play.PredictedRangeMeters / 1000f:F2} km. {defense}{crewNote}");

        return new AgentSnapshot
        {
            Sequence = ++_sequence,
            HubId = GunResource,
            HubName = "155 mm gun",
            DocumentName = $"{ProductIdentity.Title} — {ProductIdentity.Subtitle}",
            SeedHash = _play.Session.SeedHash,
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
        if (_play.Mission is not { } mission)
            return [];

        var gun = _play.Terrain.GunBaseline;
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
        var blocked = _play.FireBlockReason();
        return
        [
            Action("artillery.aim", "Aim", "Order elevation and world azimuth. The crew lays the tube on the handwheels. Trails clamp the azimuth.", AgentActionSchema.Object(["elevation", "azimuth"], ("elevation", "number"), ("azimuth", "number")), true, null),
            Action("artillery.charge", "Charge", "Order reduced, standard, max, or low (0, 1, 2, or 3). A new zone is reseated before the next shot.", AgentActionSchema.Object(["index"], ("index", "integer")), true, null),
            Action("artillery.fire", "Fire", "Shoot when the breech is closed and the tube is on the order. Returns immediately; impact arrives as artillery.impact.", null, blocked is null, blocked),
            Action("artillery.pace", "Pace", "Realtime only — fixed battle clock.", AgentActionSchema.Object(["index"], ("index", "integer")), true, null),
            Action("artillery.camera", "Camera", "Choose shell follow or gun orbit.", AgentActionSchema.Object(["mode"], ("mode", "string")), true, null),
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

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private readonly record struct TrackedImpact(BallisticTrajectoryRunner Round, string? CorrelationId);
}
