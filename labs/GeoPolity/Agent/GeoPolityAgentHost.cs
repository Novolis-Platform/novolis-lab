using System.Globalization;
using GeoPolity.Session;
using Novolis.Agent.Core;
using Novolis.Geopolitics.Core;

namespace GeoPolity.Agent;

/// <summary>Session agent host — Execute routes through <see cref="GeoSessionCommands"/>.</summary>
public sealed class GeoPolityAgentHost : IAgentHost
{
    private readonly object _gate = new();
    private readonly GeoSession _session;

    public GeoPolityAgentHost(GeoSession session) =>
        _session = session ?? throw new ArgumentNullException(nameof(session));

#pragma warning disable CS0067
    public event Action<AgentDecisionEvent>? Decision;
    public event Action<AgentChangedEvent>? Changed;
    public event Action<AgentActionResultEvent>? ActionResult;
#pragma warning restore CS0067

    public AgentHello Hello() => new()
    {
        ProtocolVersion = "1.0",
        AppId = "geopolity",
        AppTitle = "GeoPolity",
        ProcessId = Environment.ProcessId,
        SurfaceId = "geopolity",
        Description = "GeoPolity theatre: pause, speed, step, select system, military build",
        HttpPort = 18857,
        TcpPort = 18858,
        Capabilities =
        [
            AgentMethodNames.Hello,
            AgentMethodNames.Snapshot,
            AgentMethodNames.Actions,
            AgentMethodNames.Command,
            AgentMethodNames.Continue,
            AgentMethodNames.Subscribe,
        ],
    };

    public AgentSnapshot Snapshot()
    {
        lock (_gate)
        {
            var world = _session.World;
            var sim = _session.Simulation;
            var clock = _session.Clock;
            var player = _session.Player;
            var selected = _session.Selected;
            var top = string.Join(", ",
                world.Polities.OrderByDescending(p => p.PowerScore).Take(3).Select(p => p.Name));

            return new AgentSnapshot
            {
                StatusLines =
                {
                    ["year"] = world.Year.ToString(CultureInfo.InvariantCulture),
                    ["month"] = (world.Month + 1).ToString(CultureInfo.InvariantCulture),
                    ["day"] = world.Day.ToString(CultureInfo.InvariantCulture),
                    ["running"] = clock.Running ? "true" : "false",
                    ["speed"] = clock.SpeedLabel,
                    ["daysPerPulse"] = clock.DaysPerPulse.ToString(CultureInfo.InvariantCulture),
                    ["wars"] = world.ActiveWars.Count().ToString(CultureInfo.InvariantCulture),
                    ["meanLegitimacy"] = sim.Telemetry.MeanLegitimacy.ToString("0.00", CultureInfo.InvariantCulture),
                    ["meanApproval"] = sim.Telemetry.MeanApproval.ToString("0.00", CultureInfo.InvariantCulture),
                    ["headlines"] = _session.Headlines.Lines.Count.ToString(CultureInfo.InvariantCulture),
                    ["status"] = clock.StatusNote ?? "",
                    ["topPower"] = top,
                    ["player"] = $"{player.Id.Value}:{player.Name}",
                    ["selected"] = $"{selected.Id.Value}:{selected.Name}",
                    ["treasury"] = player.Treasury.ToString("0", CultureInfo.InvariantCulture),
                    ["milShare"] = player.Policy.MilitaryShare.ToString("0.00", CultureInfo.InvariantCulture),
                    ["forceLand"] = player.Military.Land.ToString("0", CultureInfo.InvariantCulture),
                    ["forceAir"] = player.Military.Air.ToString("0", CultureInfo.InvariantCulture),
                    ["forceNaval"] = player.Military.Naval.ToString("0", CultureInfo.InvariantCulture),
                },
            };
        }
    }

    public AgentActionsResponse Actions() => new()
    {
        Actions =
        [
            new AgentAction { Id = GeoPolityActionIds.Pause, Label = "Pause", Enabled = true },
            new AgentAction { Id = GeoPolityActionIds.Resume, Label = "Resume", Enabled = true },
            new AgentAction { Id = GeoPolityActionIds.Toggle, Label = "Toggle", Enabled = true },
            new AgentAction { Id = GeoPolityActionIds.SetSpeed, Label = "Set speed", Enabled = true },
            new AgentAction { Id = GeoPolityActionIds.Step, Label = "Step days", Enabled = true },
            new AgentAction { Id = GeoPolityActionIds.AdvanceYears, Label = "Advance years", Enabled = true },
            new AgentAction { Id = GeoPolityActionIds.SelectSystem, Label = "Select system", Enabled = true },
            new AgentAction { Id = GeoPolityActionIds.SetMilShare, Label = "Set mil share", Enabled = true },
            new AgentAction { Id = GeoPolityActionIds.Build, Label = "Build force", Enabled = true },
        ],
    };

    public AgentCommandResult Continue() =>
        new() { Ok = true, ActionId = AgentActionIds.Continue, Message = "no gate", Snapshot = Snapshot() };

    public void Subscribe()
    {
    }

    public AgentCommandResult Execute(AgentCommand command)
    {
        lock (_gate)
        {
            var id = command.ActionId?.Trim() ?? "";
            try
            {
                return id switch
                {
                    GeoPolityActionIds.Pause => Do(() => GeoSessionCommands.Pause(_session), id, "paused"),
                    GeoPolityActionIds.Resume => Do(() => GeoSessionCommands.Resume(_session), id, "running"),
                    GeoPolityActionIds.Toggle => DoToggle(),
                    GeoPolityActionIds.SetSpeed => DoSetSpeed(command),
                    GeoPolityActionIds.Step => DoStep(command),
                    GeoPolityActionIds.AdvanceYears => DoAdvanceYears(command),
                    GeoPolityActionIds.SelectSystem => DoSelectSystem(command),
                    GeoPolityActionIds.SetMilShare => DoSetMilShare(command),
                    GeoPolityActionIds.Build => DoBuild(command),
                    _ => Fail(id, $"unknown action '{id}'"),
                };
            }
            catch (Exception ex)
            {
                return Fail(id, ex.Message);
            }
        }
    }

    private AgentCommandResult DoToggle()
    {
        GeoSessionCommands.ToggleRun(_session);
        return Ok(GeoPolityActionIds.Toggle, _session.Clock.Running ? "running" : "paused");
    }

    private AgentCommandResult DoSetSpeed(AgentCommand command)
    {
        if (!TryGetInt(command, "preset", out var preset) && !TryGetInt(command, "value", out preset))
        {
            return Fail(GeoPolityActionIds.SetSpeed, "missing preset|1-5");
        }

        GeoSessionCommands.SetSpeed(_session, preset);
        return Ok(GeoPolityActionIds.SetSpeed, $"speed {_session.Clock.SpeedLabel}");
    }

    private AgentCommandResult DoStep(AgentCommand command)
    {
        if (!TryGetInt(command, "days", out var days))
        {
            days = 1;
        }

        GeoSessionCommands.Step(_session, days);
        return Ok(GeoPolityActionIds.Step, $"advanced {Math.Clamp(days, 1, 3650)} days to day {_session.World.Day}");
    }

    private AgentCommandResult DoAdvanceYears(AgentCommand command)
    {
        if (!TryGetInt(command, "years", out var years))
        {
            years = 1;
        }

        GeoSessionCommands.AdvanceYears(_session, years);
        return Ok(GeoPolityActionIds.AdvanceYears, $"year {_session.World.Year}");
    }

    private AgentCommandResult DoSelectSystem(AgentCommand command)
    {
        if (!TryGetInt(command, "systemId", out var id) && !TryGetInt(command, "id", out id))
        {
            return Fail(GeoPolityActionIds.SelectSystem, "missing systemId");
        }

        GeoSessionCommands.SelectSystem(_session, id);
        return Ok(GeoPolityActionIds.SelectSystem, _session.Selected.Name);
    }

    private AgentCommandResult DoSetMilShare(AgentCommand command)
    {
        if (!TryGetDouble(command, "share", out var share) && !TryGetDouble(command, "value", out share))
        {
            return Fail(GeoPolityActionIds.SetMilShare, "missing share|0.05-0.7");
        }

        GeoSessionCommands.SetMilitaryShare(_session, share);
        return Ok(GeoPolityActionIds.SetMilShare, $"mil share {_session.Player.Policy.MilitaryShare:0%}");
    }

    private AgentCommandResult DoBuild(AgentCommand command)
    {
        command.Params.TryGetValue("domain", out var domainRaw);
        if (!MilitaryBuildCosts.TryParse(domainRaw, out var domain)
            && !MilitaryBuildCosts.TryParse(command.Params.GetValueOrDefault("kind"), out domain))
        {
            return Fail(GeoPolityActionIds.Build, "missing domain|land,air,naval");
        }

        if (!TryGetDouble(command, "amount", out var amount))
        {
            amount = 10;
        }

        var before = _session.Player.Treasury;
        var msg = GeoSessionCommands.OrderBuild(_session, domain, amount);
        if (_session.Player.Treasury >= before && msg.Contains("insufficient", StringComparison.OrdinalIgnoreCase))
        {
            return Fail(GeoPolityActionIds.Build, msg);
        }

        return Ok(GeoPolityActionIds.Build, msg);
    }

    private AgentCommandResult Do(Action action, string id, string message)
    {
        action();
        return Ok(id, message);
    }

    private AgentCommandResult Ok(string id, string message) =>
        new() { Ok = true, ActionId = id, Message = message, Snapshot = Snapshot() };

    private static AgentCommandResult Fail(string id, string message) =>
        new() { Ok = false, ActionId = id, Message = message };

    private static bool TryGetInt(AgentCommand command, string key, out int value)
    {
        value = 0;
        if (command.Params.TryGetValue(key, out var s)
            && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetDouble(AgentCommand command, string key, out double value)
    {
        value = 0;
        if (command.Params.TryGetValue(key, out var s)
            && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return false;
    }
}
