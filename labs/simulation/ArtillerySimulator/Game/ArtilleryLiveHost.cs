using System.Globalization;
using Novolis.Agent.Core;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator.Game;

/// <summary>Agent host for the windowed range. Commands run on the frame thread; callers wait for the result.</summary>
internal sealed class ArtilleryLiveHost : IArtillerySurface
{
    private const string GunResource = "artillery.gun";

    private readonly ArtillerySimulatorGame _game;
    private readonly object _gate = new();
    private readonly Queue<Pending> _queue = new();
    private readonly Dictionary<string, AgentCommandResult> _completed = new(StringComparer.Ordinal);
    private Pending? _shot;
    private long _revision = 1;
    private long _sequence;
    private string _seen = "";
    private AgentReceipt? _receipt;
    private AgentLastAction? _last;

    public ArtilleryLiveHost(ArtillerySimulatorGame game)
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
                Description = "Live 155 mm range. Read the snapshot, then aim and fire.",
                HttpPort = 18770,
                TcpPort = 18771,
                Capabilities =
                [
                    "artillery.aim",
                    "artillery.charge",
                    "artillery.drag",
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
                _game.AimAt((float)elevation, (float)azimuth);
                return Accept(command, string.Create(CultureInfo.InvariantCulture, $"Aimed at {_game.Gun.ElevationDegrees:F1}° elevation, {_game.Gun.AzimuthDegrees:F1}° azimuth."));
            case "artillery.charge":
                if (!TryNumber(command, "index", out var index))
                    return Reject(command, "invalid-arguments", "Charge needs an index.");
                _game.Gun.SetCharge((int)index);
                return Accept(command, string.Create(CultureInfo.InvariantCulture, $"Charge {_game.Gun.ChargeLabel} ({_game.Gun.MuzzleSpeedMps:F0} m/s)."));
            case "artillery.drag":
                if (!TryBool(command, "enabled", out var drag))
                    return Reject(command, "invalid-arguments", "Drag needs enabled true or false.");
                _game.SetDrag(drag);
                return Accept(command, _game.Gun.DragEnabled ? "Drag on." : "Vacuum.");
            case "artillery.pace":
                if (!TryNumber(command, "index", out var pace))
                    return Reject(command, "invalid-arguments", "Pace needs an index.");
                _game.SetPace((int)pace);
                return Accept(command, _game.PaceText);
            case "artillery.camera":
                if (!command.Params.TryGetValue("mode", out var mode))
                    return Reject(command, "invalid-arguments", "Camera needs mode follow or free.");
                var follow = mode.Equals("follow", StringComparison.OrdinalIgnoreCase) || mode.Equals("orbit", StringComparison.OrdinalIgnoreCase);
                _game.SetFollowCamera(follow);
                return Accept(command, follow ? "Camera follows the arc." : "Camera is free.");
            case "artillery.fire":
                if (!_game.TryFire())
                    return Reject(command, "unavailable", "A shot is already in flight.");
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
                $"Impact {hit.HorizontalRangeMeters / 1000f:F2} km after {hit.TimeSeconds:F1} s ({hit.Reason}), {hit.ImpactSpeedMps:F0} m/s.")
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
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{shot.Phase}|{gun.ElevationDegrees:F0}|{gun.AzimuthDegrees:F0}|{gun.ChargeIndex}|{gun.DragEnabled}|{_game.CameraMode}|{_game.PaceText}|{shot.Impact?.HorizontalRangeMeters:F0}");
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
            ["drag"] = gun.DragEnabled ? "on" : "off",
            ["phase"] = shot.Phase.ToString(),
            ["camera"] = _game.CameraMode.ToString(),
            ["pace"] = _game.PaceText,
            ["predictedMeters"] = _game.PredictedRangeMeters.ToString("F0", CultureInfo.InvariantCulture),
            ["terrain"] = terrain.IsFlat ? "flat" : terrain.Style.ToString(),
        };
        if (impact is { } hit)
        {
            lines["rangeMeters"] = hit.HorizontalRangeMeters.ToString("F0", CultureInfo.InvariantCulture);
            lines["flightSeconds"] = hit.TimeSeconds.ToString("F1", CultureInfo.InvariantCulture);
            lines["impact"] = hit.Reason.ToString();
        }

        var summary = impact is { } recorded
            ? string.Create(CultureInfo.InvariantCulture, $"{shot.Phase}: aim {gun.ElevationDegrees:F0}° {gun.ChargeLabel}, impact {recorded.HorizontalRangeMeters / 1000f:F2} km ({recorded.Reason}). Camera {_game.CameraMode}.")
            : string.Create(CultureInfo.InvariantCulture, $"{shot.Phase}: aim {gun.ElevationDegrees:F0}° / {gun.AzimuthDegrees:F0}°, {gun.ChargeLabel}, predicted {_game.PredictedRangeMeters / 1000f:F2} km. Camera {_game.CameraMode}.");

        return new AgentSnapshot
        {
            Sequence = ++_sequence,
            HubId = GunResource,
            HubName = "155 mm gun",
            DocumentName = "Artillery range",
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
        };
    }

    private AgentAction[] Catalog()
    {
        var flying = _game.Shot.Phase == BallisticTrajectoryPhase.InFlight;
        return
        [
            Action("artillery.aim", "Aim", "Set barrel elevation and azimuth in degrees.", AgentActionSchema.Object(["elevation", "azimuth"], ("elevation", "number"), ("azimuth", "number")), true, null),
            Action("artillery.charge", "Charge", "Select reduced, standard, or max charge.", AgentActionSchema.Object(["index"], ("index", "integer")), true, null),
            Action("artillery.drag", "Drag", "Turn aerodynamic drag on or off.", AgentActionSchema.Object(["enabled"], ("enabled", "boolean")), true, null),
            Action("artillery.fire", "Fire", "Shoot and wait until the round lands.", null, !flying, flying ? "A shot is already in flight." : null),
            Action("artillery.pace", "Pace", "Set flight pace 0..3.", AgentActionSchema.Object(["index"], ("index", "integer")), true, null),
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
