using System.Text.Json;
using CalypsoCad.Generation;
using CalypsoCad.Models;
using CalypsoCad.Services;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Agent;

internal sealed class CalypsoCadAgentHost : IAgentHost
{
    private readonly AgentSurfaceDefinition _definition;
    private readonly object _gate = new();
    private readonly Dictionary<string, AgentCommandResult> _completed = new(StringComparer.Ordinal);
    private long _revision = 1;
    private string _phase = "lock";

    public CalypsoCadAgentHost(AgentSurfaceDefinition? definition = null)
    {
        _definition = definition ?? AgentSurfaceDefinition.From<ICalypsoCadSurface>();
    }

    public event Action<AgentDecisionEvent>? Decision;

    public event Action<AgentChangedEvent>? Changed;

    public event Action<AgentActionResultEvent>? ActionResult;

    public AgentHello Hello() => _definition.BuildHello(appId: "calypso-cad", appTitle: "Calypso CAD");

    public AgentSnapshot Snapshot()
    {
        lock (_gate)
            return Capture();
    }

    public AgentActionsResponse Actions() => new() { Actions = _definition.Actions.ToArray() };

    public AgentCommandResult Continue()
    {
        lock (_gate)
        {
            var result = Accept(new AgentCommand { ActionId = AgentActionIds.Continue }, "continue", mutate: false);
            Decision?.Invoke(new AgentDecisionEvent { DecisionLine = _phase, Snapshot = result.Snapshot });
            return result;
        }
    }

    public void Subscribe()
    {
    }

    public AgentCommandResult Execute(AgentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (_gate)
        {
            var actionId = command.ActionId?.Trim() ?? "";
            if (actionId.Length == 0)
                return Reject(command, "incomplete", "ActionId required");

            if (!string.IsNullOrWhiteSpace(command.CorrelationId)
                && _completed.TryGetValue(command.CorrelationId, out var prior))
                return prior;

            var action = _definition.Actions.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, actionId, StringComparison.Ordinal));
            var rejected = AgentCommandGate.Reject(action, command, _revision);
            if (rejected is not null)
            {
                Publish(rejected);
                return rejected;
            }

            try
            {
                var result = actionId switch
                {
                    CalypsoCadActionIds.Lock => Lock(command),
                    CalypsoCadActionIds.Describe => Describe(command),
                    CalypsoCadActionIds.Validate => Validate(command),
                    CalypsoCadActionIds.Dump => Dump(command),
                    CalypsoCadActionIds.Generate => Generate(command),
                    CalypsoCadActionIds.Outfit => Outfit(command),
                    CalypsoCadActionIds.Structure => Structure(command),
                    _ => Reject(command, "unknown", $"Unknown action '{actionId}'."),
                };
                if (!string.IsNullOrWhiteSpace(result.CorrelationId))
                    _completed[result.CorrelationId] = result;
                Publish(result);
                return result;
            }
            catch (Exception ex)
            {
                var failed = Reject(command, "failed", ex.Message);
                Publish(failed);
                return failed;
            }
        }
    }

    private AgentCommandResult Lock(AgentCommand command)
    {
        _phase = "lock";
        return Accept(command, CalypsoLockGenerator.DescribeLock(), mutate: false);
    }

    private AgentCommandResult Describe(AgentCommand command)
    {
        _phase = "describe";
        if (!TryReadDocument(out var doc, out var path))
            return Accept(command, "No calypso.cadjson yet. Run calypso.generate.", mutate: false);

        var outfit = doc.Entities.Where(e => e.Name?.StartsWith("outfit-", StringComparison.OrdinalIgnoreCase) == true).Select(e => e.Name).ToArray();
        var frames = doc.Entities.Count(e => e.Name?.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) == true);
        var missing = new[] { "BRIDGE", "CREW_1", "GALLEY", "ENG", "HOLD" }
            .Where(id => !outfit.Any(name => name!.StartsWith($"outfit-{id}", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var mass = Novolis.Ship.Structure.ShipStructureDocument.TryGetMass(doc, out var skin) && skin is not null
            ? skin.MassKg.ToString("0")
            : "none";
        var message = $"path={path}{Environment.NewLine}entities={doc.Entities.Count} frames={frames} outfit={outfit.Length} massKg={mass}";
        if (missing.Length > 0)
            message += $"{Environment.NewLine}missingOutfit={string.Join(",", missing)}";
        return Accept(command, message, mutate: false, resource: path);
    }

    private AgentCommandResult Validate(AgentCommand command)
    {
        _phase = "validate";
        if (!TryReadDocument(out var doc, out var path))
            return Reject(command, "not-found", "No calypso.cadjson yet. Run calypso.generate.");

        var failed = CalypsoAcceptance.Evaluate(doc).Where(check => !check.Ok).ToList();
        if (failed.Count == 0)
            return Accept(command, "validate ok", mutate: false, resource: path);

        var message = string.Join(Environment.NewLine, failed.Select(check => $"{check.Name}: {check.Detail}"));
        return Reject(command, "invalid", message);
    }

    private AgentCommandResult Dump(AgentCommand command)
    {
        _phase = "dump";
        var session = new CalypsoSession();
        if (!session.TryLoadExisting())
            return Reject(command, "not-found", "No calypso.cadjson yet. Run calypso.generate.");

        var kind = command.Get("kind") ?? "orbit";
        var preset = command.Get("preset");
        var compartment = command.Get("compartment");
        var renderer = new CalypsoRenderer(session);
        renderer.Fit();
        var path = HeadlessPngExporter.ExportOne(session, renderer, session.GeneratedDirectory, kind, preset, compartment);
        if (path is null)
            return Reject(command, "failed", "Headless PNG export produced no frame.");

        var result = Accept(command, path, mutate: false, resource: path);
        result.Receipt!.Artifacts =
        [
            new AgentArtifactRef
            {
                Id = Path.GetFileName(path),
                Kind = "png",
                MediaType = "image/png",
                Uri = path,
                Summary = kind,
            },
        ];
        return result;
    }

    private AgentCommandResult Generate(AgentCommand command)
    {
        _phase = "generate";
        var dir = CalypsoLockGenerator.Generate();
        var path = Path.Combine(dir, "calypso.cadjson");
        var result = Accept(command, path, mutate: true, resource: path);
        result.Receipt!.Artifacts =
        [
            new AgentArtifactRef { Id = "calypso.cadjson", Kind = "cadjson", MediaType = "application/json", Uri = path, Summary = "Lock companions" },
        ];
        return result;
    }

    private AgentCommandResult Outfit(AgentCommand command)
    {
        _phase = "outfit";
        var dir = CalypsoLockGenerator.Generate();
        var doc = ReadDocument(dir);
        var compartment = command.Get("compartment");
        var names = doc.Entities
            .Select(e => e.Name)
            .Where(name => name?.StartsWith("outfit-", StringComparison.OrdinalIgnoreCase) == true)
            .Where(name => compartment is null || name!.Contains(compartment, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (names.Length == 0)
            return Reject(command, "not-found", compartment is null ? "Outfit schedule produced no boxes." : $"No outfit for '{compartment}'.");

        return Accept(command, string.Join(Environment.NewLine, names), mutate: true, resource: Path.Combine(dir, "calypso.cadjson"));
    }

    private AgentCommandResult Structure(AgentCommand command)
    {
        _phase = "structure";
        var dir = CalypsoLockGenerator.Generate();
        var doc = ReadDocument(dir);
        var names = doc.Entities
            .Select(e => e.Name)
            .Where(name => name is not null && (
                name.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("stringer-", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("coaming-", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "ramp-aft", StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (names.Length == 0)
            return Reject(command, "not-found", "Structure schedule produced no solids.");

        return Accept(command, string.Join(Environment.NewLine, names), mutate: true, resource: Path.Combine(dir, "calypso.cadjson"));
    }

    private static bool TryReadDocument(out CadDocument doc, out string path)
    {
        path = Path.Combine(CalypsoLockGenerator.DefaultOutputDirectory, "calypso.cadjson");
        if (!File.Exists(path))
        {
            doc = new CadDocument();
            return false;
        }

        doc = JsonSerializer.Deserialize<CadDocument>(File.ReadAllText(path), CadJson.Options)
              ?? throw new InvalidOperationException("Failed to deserialize calypso.cadjson");
        return true;
    }

    private static CadDocument ReadDocument(string dir)
    {
        var path = Path.Combine(dir, "calypso.cadjson");
        return JsonSerializer.Deserialize<CadDocument>(File.ReadAllText(path), CadJson.Options)
               ?? throw new InvalidOperationException("Failed to deserialize calypso.cadjson");
    }

    private AgentCommandResult Accept(AgentCommand command, string message, bool mutate, string? resource = null)
    {
        if (mutate)
            _revision++;

        var receipt = AgentCommandGate.CreateReceipt(command, AgentLifecycle.Applied, true, message, _revision, recovery: "Refresh the snapshot before the next mutation.");
        return new AgentCommandResult
        {
            Ok = true,
            ActionId = command.ActionId,
            CorrelationId = command.CorrelationId,
            Message = message,
            StateRevision = _revision,
            Receipt = receipt,
            Snapshot = Capture(resource),
        };
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
            "Refresh the snapshot, then retry with a new correlation or the required approval token.");
        var result = new AgentCommandResult
        {
            Ok = false,
            ActionId = command.ActionId,
            CorrelationId = command.CorrelationId,
            ErrorCode = reason,
            Message = message,
            StateRevision = _revision,
            Receipt = receipt,
            Snapshot = Capture(),
        };
        if (!string.IsNullOrWhiteSpace(command.CorrelationId) && reason is not "stale-snapshot" and not "approval-required" and not "invalid-arguments")
            _completed[command.CorrelationId] = result;
        return result;
    }

    private void Publish(AgentCommandResult result)
    {
        var evt = new AgentActionResultEvent
        {
            ActionId = result.ActionId,
            Ok = result.Ok,
            Message = result.Message,
            ErrorCode = result.ErrorCode,
            CorrelationId = result.CorrelationId,
            ResourceId = result.Receipt?.Effects.FirstOrDefault()?.ResourceId,
            Severity = result.Ok ? AgentSeverity.Info : AgentSeverity.Warning,
            Receipt = result.Receipt,
            Snapshot = result.Snapshot,
        };
        ActionResult?.Invoke(evt);
        Changed?.Invoke(new AgentChangedEvent
        {
            Reason = result.Ok ? "applied" : "rejected",
            CorrelationId = result.CorrelationId,
            Severity = evt.Severity,
            Snapshot = result.Snapshot,
        });
    }

    private AgentSnapshot Capture(string? resource = null) => new()
    {
        StateRevision = _revision,
        MetadataVersion = AgentMetadata.Version,
        DocumentName = "Calypso",
        PauseReason = _phase,
        Actions = _definition.Actions.ToArray(),
        Observations =
        [
            new AgentObservation
            {
                ResourceId = resource ?? "calypso",
                StateRevision = _revision,
                Summary = _phase,
                AvailableActionIds = _definition.Actions.Select(action => action.Id).ToArray(),
            },
        ],
    };
}
