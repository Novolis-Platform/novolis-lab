using System.Globalization;
using Novolis.Agent.Core;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator.Game;

/// <summary>Headless control of the artillery range through the generic agent contract.</summary>
internal sealed class ArtilleryAgentHost : IAgentHost
{
    private const string GunResource = "artillery.gun";

    private readonly object _gate = new();
    private readonly Dictionary<string, AgentCommandResult> _completed = new(StringComparer.Ordinal);
    private readonly TerrainWorld _terrain = new();
    private readonly GunModel _gun = new();
    private readonly BallisticTrajectoryRunner _shot = new(new BallisticTrajectoryRunnerOptions
    {
        Step = new ProjectileTerrainStepOptions
        {
            DtSeconds = 1.0 / 120.0,
            ProjectileRadius = GunModel.MuzzleRadius,
        },
    });
    private readonly ProjectileBallisticSimulation _vacuum = new();
    private readonly ProjectileBallisticSimulation _drag = new(GunModel.Educational155Profile);
    private readonly AtmosphereModel _atmosphere = AtmosphereModel.CreateRegionalDefault();
    private long _revision = 1;
    private long _sequence;
    private AgentReceipt? _receipt;
    private AgentLastAction? _last;

    public ArtilleryAgentHost()
    {
        _terrain.Rebuild(42, flat: false, TerrainStyle.Rugged);
    }

#pragma warning disable CS0067 // The range has no decision gate, so nothing raises this event.
    public event Action<AgentDecisionEvent>? Decision;
#pragma warning restore CS0067

    public event Action<AgentChangedEvent>? Changed;

    public event Action<AgentActionResultEvent>? ActionResult;

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
                Description = "Educational 155 mm range. Aim, choose a charge, and fire.",
                Capabilities =
                [
                    "artillery.aim",
                    "artillery.charge",
                    "artillery.fire",
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
        {
            return Capture();
        }
    }

    public AgentActionsResponse Actions()
    {
        lock (_gate)
        {
            return new AgentActionsResponse { Sequence = ++_sequence, Actions = Catalog() };
        }
    }

    public AgentCommandResult Execute(AgentCommand command)
    {
        AgentCommandResult result;
        AgentChangedEvent? changed = null;
        AgentActionResultEvent? completed = null;
        lock (_gate)
        {
            if (!string.IsNullOrEmpty(command.CorrelationId)
                && _completed.TryGetValue(command.CorrelationId, out var prior))
            {
                return prior;
            }

            var action = Catalog().FirstOrDefault(candidate => candidate.Id == command.ActionId);
            var rejected = AgentCommandGate.Reject(action, command, _revision);
            if (rejected is not null)
            {
                return rejected;
            }

            if (action is not { Enabled: true })
            {
                return Reject(command, "unavailable", action?.DisabledReason ?? "Unknown action.");
            }

            var message = Apply(command);
            if (message is null)
            {
                return Reject(command, "invalid-arguments", "The action arguments could not be applied.");
            }

            _revision++;
            _receipt = AgentCommandGate.CreateReceipt(
                command,
                AgentLifecycle.Applied,
                true,
                message,
                _revision,
                effects:
                [
                    new AgentEffect
                    {
                        ResourceId = GunResource,
                        Kind = command.ActionId,
                        After = message,
                    },
                ]);
            _last = new AgentLastAction { ActionId = command.ActionId, Ok = true, Message = message };
            var snapshot = Capture();
            result = new AgentCommandResult
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
            if (!string.IsNullOrEmpty(command.CorrelationId))
            {
                _completed[command.CorrelationId] = result;
            }

            changed = new AgentChangedEvent
            {
                Sequence = _sequence,
                Reason = command.ActionId,
                CorrelationId = command.CorrelationId,
                ResourceId = GunResource,
                Severity = AgentSeverity.Info,
                Snapshot = snapshot,
            };
            completed = new AgentActionResultEvent
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
            };
        }

        Changed?.Invoke(changed);
        ActionResult?.Invoke(completed);
        return result;
    }

    public AgentCommandResult Continue() =>
        new() { Ok = true, Message = "The range has no decision gate.", StateRevision = _revision };

    public void Subscribe()
    {
    }

    private string? Apply(AgentCommand command)
    {
        switch (command.ActionId)
        {
            case "artillery.aim":
                if (!TryNumber(command, "elevation", out var elevation) || !TryNumber(command, "azimuth", out var azimuth))
                {
                    return null;
                }

                _gun.NudgeElevation((float)elevation - _gun.ElevationDegrees);
                _gun.NudgeAzimuth((float)azimuth - _gun.AzimuthDegrees);
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"Aimed at {_gun.ElevationDegrees:F1}° elevation, {_gun.AzimuthDegrees:F1}° azimuth.");
            case "artillery.charge":
                if (!TryNumber(command, "index", out var index))
                {
                    return null;
                }

                _gun.SetCharge((int)index);
                return string.Create(CultureInfo.InvariantCulture, $"Charge {_gun.ChargeLabel} ({_gun.MuzzleSpeedMps:F0} m/s).");
            case "artillery.fire":
                return Fire();
            default:
                return null;
        }
    }

    private string Fire()
    {
        var muzzle = _gun.MuzzlePosition(_terrain.GunBaseline);
        _shot.Begin(_gun.CreateProjectileState(muzzle, _gun.BarrelDirection()));
        var simulation = _gun.DragEnabled ? _drag : _vacuum;
        var guard = 0;
        while (_shot.Phase == BallisticTrajectoryPhase.InFlight && guard++ < 4_000)
        {
            var environment = _atmosphere.BallisticEnvironment(_gun.DragEnabled, _shot.CurrentPosition.Y);
            _shot.AdvanceWithBudget(
                simulation,
                environment,
                _terrain.Collision,
                _terrain.Field,
                _terrain.GunBaseline,
                maxPhysicsSteps: 32);
        }

        if (_shot.Impact is not { } impact)
        {
            return "The shot did not impact.";
        }

        var range = impact.HorizontalRangeMeters >= 1000f
            ? string.Create(CultureInfo.InvariantCulture, $"{impact.HorizontalRangeMeters / 1000f:F2} km")
            : string.Create(CultureInfo.InvariantCulture, $"{impact.HorizontalRangeMeters:F0} m");
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Impact {range} after {impact.TimeSeconds:F1} s ({impact.Reason}), {impact.ImpactSpeedMps:F0} m/s.");
    }

    private AgentCommandResult Reject(AgentCommand command, string reason, string message)
    {
        var receipt = AgentCommandGate.CreateReceipt(
            command,
            AgentLifecycle.Rejected,
            false,
            message,
            _revision,
            reason,
            message);
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
        var impact = _shot.Impact;
        var phase = _shot.Phase.ToString();
        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["elevation"] = _gun.ElevationDegrees.ToString("F1", CultureInfo.InvariantCulture),
            ["azimuth"] = _gun.AzimuthDegrees.ToString("F1", CultureInfo.InvariantCulture),
            ["charge"] = _gun.ChargeLabel,
            ["muzzleMps"] = _gun.MuzzleSpeedMps.ToString("F0", CultureInfo.InvariantCulture),
            ["drag"] = _gun.DragEnabled ? "on" : "off",
            ["phase"] = phase,
            ["atmosphere"] = AtmosphereLine(),
        };
        if (impact is { } hit)
        {
            lines["rangeMeters"] = hit.HorizontalRangeMeters.ToString("F0", CultureInfo.InvariantCulture);
            lines["flightSeconds"] = hit.TimeSeconds.ToString("F1", CultureInfo.InvariantCulture);
            lines["impact"] = hit.Reason.ToString();
        }

        return new AgentSnapshot
        {
            Sequence = ++_sequence,
            HubId = GunResource,
            HubName = "155 mm gun",
            DocumentName = "Artillery range",
            SeedHash = "42",
            PauseReason = phase,
            StateRevision = _revision,
            MetadataVersion = AgentMetadata.Version,
            StatusLines = lines,
            MapVisible = true,
            MapX = impact?.Position.X ?? _terrain.GunBaseline.X,
            MapY = impact?.Position.Z ?? _terrain.GunBaseline.Z,
            Actions = Catalog(),
            Receipt = _receipt,
            LastAction = _last,
            Observations =
            [
                new AgentObservation
                {
                    ResourceId = GunResource,
                    StateRevision = _revision,
                    Summary = impact is { } recorded
                        ? string.Create(CultureInfo.InvariantCulture, $"{phase}: {recorded.HorizontalRangeMeters / 1000f:F2} km ({recorded.Reason})")
                        : string.Create(CultureInfo.InvariantCulture, $"{phase}, {_gun.ElevationDegrees:F1}°, {_gun.ChargeLabel}"),
                    AvailableActionIds = Catalog().Where(action => action.Enabled).Select(action => action.Id).ToArray(),
                },
            ],
        };
    }

    private AgentAction[] Catalog()
    {
        var flying = _shot.Phase == BallisticTrajectoryPhase.InFlight;
        return
        [
            Action(
                "artillery.aim",
                "Aim",
                "Set barrel elevation and azimuth in degrees.",
                "Moves the barrel. The next shot uses this aim.",
                AgentActionSchema.Object(["elevation", "azimuth"], ("elevation", "number"), ("azimuth", "number")),
                enabled: true,
                disabledReason: null),
            Action(
                "artillery.charge",
                "Charge",
                "Select reduced, standard, or max charge (0, 1, or 2).",
                "Changes muzzle velocity for the next shot.",
                AgentActionSchema.Object(["index"], ("index", "integer")),
                enabled: true,
                disabledReason: null),
            Action(
                "artillery.fire",
                "Fire",
                "Shoot the current aim and charge and wait for impact.",
                "Integrates the shot against the range until it lands.",
                schema: null,
                enabled: !flying,
                disabledReason: flying ? "A shot is already in flight." : null),
        ];
    }

    private static AgentAction Action(
        string id,
        string label,
        string summary,
        string preview,
        Dictionary<string, object?>? schema,
        bool enabled,
        string? disabledReason) => new()
        {
            Id = id,
            Label = label,
            Summary = summary,
            EffectPreview = preview,
            Schema = schema,
            Enabled = enabled,
            DisabledReason = disabledReason,
            Capability = "artillery.play",
            SafetyClass = AgentSafetyClass.Mutate,
            ApprovalRequired = false,
            Idempotency = "correlation",
            MetadataVersion = AgentMetadata.Version,
        };

    private string AtmosphereLine()
    {
        var rho = _atmosphere.DensityKgPerM3At(_terrain.GunBaseline.Y);
        var wind = _atmosphere.SurfaceWindMetersPerSecond;
        return string.Create(CultureInfo.InvariantCulture, $"rho {rho:F3} kg/m3  wind {wind.X:F0} {wind.Z:F0} m/s");
    }

    private static bool TryNumber(AgentCommand command, string name, out double value)
    {
        value = 0;
        return command.Params.TryGetValue(name, out var text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
