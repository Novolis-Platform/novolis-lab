using System.Globalization;
using System.Text;
using KatoriLab.Demo;
using Novolis.Agent.Core;

namespace KatoriLab.Agent;

internal sealed class KatoriLabAgentHost : IAgentHost
{
    readonly KatoriKataDriver _driver;
    readonly object _gate = new();

    public KatoriLabAgentHost(KatoriKataDriver driver) =>
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));

#pragma warning disable CS0067
    public event Action<AgentDecisionEvent>? Decision;
    public event Action<AgentChangedEvent>? Changed;
    public event Action<AgentActionResultEvent>? ActionResult;
#pragma warning restore CS0067

    public AgentHello Hello() => new()
    {
        ProtocolVersion = "1.0",
        AppId = "katorilab",
        AppTitle = "KatoriLab — ken kata & bokken holds",
        ProcessId = Environment.ProcessId,
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
            var s = _driver.SamplePose();
            return new AgentSnapshot
            {
                StatusLines =
                {
                    ["phase"] = s.Phase,
                    ["time"] = s.Time.ToString("0.###", CultureInfo.InvariantCulture),
                    ["paused"] = _driver.Paused ? "true" : "false",
                    ["clip"] = _driver.ClipId,
                    ["source"] = _driver.SkinSource,
                    ["holdMode"] = _driver.HoldMode ? "true" : "false",
                    ["mode"] = "katori-ken-wire",
                },
            };
        }
    }

    public AgentActionsResponse Actions() => new()
    {
        Actions =
        [
            new AgentAction { Id = KatoriLabActionIds.ListPhases, Label = "List phases", Enabled = true },
            new AgentAction { Id = KatoriLabActionIds.SamplePhase, Label = "Sample phase", Enabled = true },
            new AgentAction { Id = KatoriLabActionIds.SetPhaseTime, Label = "Seek", Enabled = true },
            new AgentAction { Id = KatoriLabActionIds.MeasureVertexDelta, Label = "Measure Δ", Enabled = true },
            new AgentAction { Id = KatoriLabActionIds.MeasureBoneTravel, Label = "Bone travel", Enabled = true },
            new AgentAction { Id = KatoriLabActionIds.SampleHolds, Label = "Sample holds", Enabled = true },
            new AgentAction { Id = KatoriLabActionIds.Diagnose, Label = "Diagnose kamae", Enabled = true },
            new AgentAction { Id = KatoriLabActionIds.Explore, Label = "Explore", Enabled = true },
            new AgentAction { Id = KatoriLabActionIds.Pause, Label = "Pause", Enabled = true },
            new AgentAction { Id = KatoriLabActionIds.Resume, Label = "Resume", Enabled = true },
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
                    KatoriLabActionIds.ListPhases => DoListPhases(),
                    KatoriLabActionIds.SamplePhase => DoSample(command),
                    KatoriLabActionIds.SetPhaseTime => DoSeek(command),
                    KatoriLabActionIds.MeasureVertexDelta => DoMeasure(command),
                    KatoriLabActionIds.MeasureBoneTravel => DoBoneTravel(command),
                    KatoriLabActionIds.SampleHolds => DoSampleHolds(command),
                    KatoriLabActionIds.Diagnose => DoDiagnose(command),
                    KatoriLabActionIds.Explore => DoExplore(),
                    KatoriLabActionIds.Pause => DoPause(true),
                    KatoriLabActionIds.Resume => DoPause(false),
                    _ => Fail(id, $"Unknown action '{id}'."),
                };
            }
            catch (Exception ex)
            {
                return Fail(id, ex.Message);
            }
        }
    }

    AgentCommandResult DoListPhases()
    {
        var sb = new StringBuilder("clip=katori-ken;");
        foreach (var (id, time, label) in KatoriKataClips.Phases)
            sb.Append($" {id}@{time:0.#}s={label};");
        return Ok(KatoriLabActionIds.ListPhases, sb.ToString().TrimEnd(';'));
    }

    AgentCommandResult DoSample(AgentCommand command)
    {
        ApplySeek(command);
        var s = _driver.SamplePose();
        return Ok(
            KatoriLabActionIds.SamplePhase,
            Inv(
                $"phase={s.Phase}; time={s.Time:0.###}; hips=({s.Hips.X:0.###},{s.Hips.Y:0.###},{s.Hips.Z:0.###}); head=({s.Head.X:0.###},{s.Head.Y:0.###},{s.Head.Z:0.###}); rHand=({s.RightHand.X:0.###},{s.RightHand.Y:0.###},{s.RightHand.Z:0.###}); lHand=({s.LeftHand.X:0.###},{s.LeftHand.Y:0.###},{s.LeftHand.Z:0.###}); kissaki=({s.Kissaki.X:0.###},{s.Kissaki.Y:0.###},{s.Kissaki.Z:0.###}); kashira=({s.Kashira.X:0.###},{s.Kashira.Y:0.###},{s.Kashira.Z:0.###})"));
    }

    AgentCommandResult DoSeek(AgentCommand command)
    {
        ApplySeek(command);
        return Ok(KatoriLabActionIds.SetPhaseTime, Inv($"phase={_driver.Phase}; time={_driver.TimeSeconds:0.###}"));
    }

    AgentCommandResult DoMeasure(AgentCommand command)
    {
        ResolvePair(command, out var a, out var b);
        var d = _driver.MeasureVertexDelta(a, b);
        return Ok(
            KatoriLabActionIds.MeasureVertexDelta,
            Inv(
                $"A={d.PhaseA}@{d.TimeA:0.###}; B={d.PhaseB}@{d.TimeB:0.###}; max={d.MaxDelta:0.####}; mean={d.MeanDelta:0.####}; upper={d.UpperBodyMaxDelta:0.####}"));
    }

    AgentCommandResult DoBoneTravel(AgentCommand command)
    {
        ResolvePair(command, out var a, out var b);
        var t = _driver.MeasureBoneTravel(a, b);
        return Ok(
            KatoriLabActionIds.MeasureBoneTravel,
            Inv(
                $"A={t.PhaseA}; B={t.PhaseB}; rHand={t.RightHand:0.####}; lHand={t.LeftHand:0.####}; head={t.Head:0.####}; hips={t.Hips:0.####}"));
    }

    AgentCommandResult DoSampleHolds(AgentCommand command)
    {
        ApplySeek(command);
        var h = _driver.SampleHolds();
        return Ok(
            KatoriLabActionIds.SampleHolds,
            Inv(
                $"phase={h.Phase}; time={h.Time:0.###}; rErr={h.RightHandError:0.####}; lErr={h.LeftHandError:0.####}; primary=({h.PrimaryHold.X:0.###},{h.PrimaryHold.Y:0.###},{h.PrimaryHold.Z:0.###}); secondary=({h.SecondaryHold.X:0.###},{h.SecondaryHold.Y:0.###},{h.SecondaryHold.Z:0.###})"));
    }

    AgentCommandResult DoDiagnose(AgentCommand command)
    {
        ApplySeek(command);
        return Ok(KatoriLabActionIds.Diagnose, _driver.Diagnose());
    }

    AgentCommandResult DoExplore()
    {
        _driver.HoldMode = true;
        var delta = _driver.MeasureVertexDelta(
            KatoriKataClips.TimeForPhase("chudan"),
            KatoriKataClips.TimeForPhase("jodan"));
        _driver.SeekPhase("chudan");
        var chudan = _driver.Diagnose();
        _driver.SeekPhase("jodan");
        var jodan = _driver.Diagnose();
        return Ok(
            KatoriLabActionIds.Explore,
            Inv($"travel max={delta.MaxDelta:0.####} upper={delta.UpperBodyMaxDelta:0.####} || CHUDAN: {chudan} || JODAN: {jodan}"));
    }

    AgentCommandResult DoPause(bool paused)
    {
        _driver.Paused = paused;
        return Ok(paused ? KatoriLabActionIds.Pause : KatoriLabActionIds.Resume, paused ? "paused" : "resumed");
    }

    void ApplySeek(AgentCommand command)
    {
        if (TryGet(command, "time", out var timeStr) &&
            float.TryParse(timeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var time))
        {
            _driver.Seek(time);
            return;
        }

        if (TryGet(command, "phase", out var phase))
            _driver.SeekPhase(phase);
    }

    void ResolvePair(AgentCommand command, out float a, out float b)
    {
        a = KatoriKataClips.TimeForPhase("chudan");
        b = KatoriKataClips.TimeForPhase("jodan");
        if (TryGet(command, "timeA", out var ta) &&
            float.TryParse(ta, NumberStyles.Float, CultureInfo.InvariantCulture, out var fa))
            a = fa;
        else if (TryGet(command, "phaseA", out var pa))
            a = KatoriKataClips.TimeForPhase(pa);

        if (TryGet(command, "timeB", out var tb) &&
            float.TryParse(tb, NumberStyles.Float, CultureInfo.InvariantCulture, out var fb))
            b = fb;
        else if (TryGet(command, "phaseB", out var pb))
            b = KatoriKataClips.TimeForPhase(pb);
    }

    static bool TryGet(AgentCommand command, string key, out string value)
    {
        value = "";
        if (!command.Params.TryGetValue(key, out var raw) || string.IsNullOrEmpty(raw))
            return false;
        value = raw;
        return true;
    }

    AgentCommandResult Ok(string id, string message) =>
        new() { Ok = true, ActionId = id, Message = message, Snapshot = Snapshot() };

    static AgentCommandResult Fail(string id, string message) =>
        new() { Ok = false, ActionId = id, Message = message };

    static string Inv(FormattableString fs) => FormattableString.Invariant(fs);
}
