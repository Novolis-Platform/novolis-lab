using System.Globalization;
using System.Text;
using CharacterLab.Demo;
using Novolis.Agent.Core;

namespace CharacterLab.Agent;

/// <summary>Agent host wrapping <see cref="MocapParadeDriver"/> for exploration.</summary>
internal sealed class CharacterLabAgentHost : IAgentHost
{
    readonly MocapParadeDriver _driver;
    readonly object _gate = new();

    public CharacterLabAgentHost(MocapParadeDriver driver) =>
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));

#pragma warning disable CS0067
    public event Action<AgentDecisionEvent>? Decision;
    public event Action<AgentChangedEvent>? Changed;
    public event Action<AgentActionResultEvent>? ActionResult;
#pragma warning restore CS0067

    public AgentHello Hello() => new()
    {
        ProtocolVersion = "1.0",
        AppId = "characterlab",
        AppTitle = "CharacterLab — mocap wire & hold points",
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
            var clip = _driver.ActiveClip;
            return new AgentSnapshot
            {
                StatusLines =
                {
                    ["phase"] = s.Phase,
                    ["time"] = s.Time.ToString("0.###", CultureInfo.InvariantCulture),
                    ["paused"] = _driver.Paused ? "true" : "false",
                    ["clip"] = clip?.Id ?? _driver.ActiveClipId,
                    ["source"] = _driver.SkinSource,
                    ["holdMode"] = _driver.HoldMode ? "true" : "false",
                    ["mode"] = "mocap-wire",
                },
            };
        }
    }

    public AgentActionsResponse Actions() => new()
    {
        Actions =
        [
            new AgentAction { Id = CharacterLabActionIds.SkinStats, Label = "Skin stats", Summary = "Clip source + bones", Enabled = true },
            new AgentAction { Id = CharacterLabActionIds.BoneCoverage, Label = "Bone coverage", Enabled = true },
            new AgentAction { Id = CharacterLabActionIds.ListPhases, Label = "List clips/phases", Enabled = true },
            new AgentAction { Id = CharacterLabActionIds.SamplePhase, Label = "Sample phase", Summary = "Bone tips", Enabled = true },
            new AgentAction { Id = CharacterLabActionIds.SetPhaseTime, Label = "Seek", Enabled = true },
            new AgentAction { Id = CharacterLabActionIds.MeasureVertexDelta, Label = "Measure Δ", Summary = "Joint tip deltas", Enabled = true },
            new AgentAction { Id = CharacterLabActionIds.MeasureBoneTravel, Label = "Bone travel", Enabled = true },
            new AgentAction { Id = CharacterLabActionIds.SampleHolds, Label = "Sample holds", Enabled = true },
            new AgentAction { Id = CharacterLabActionIds.Explore, Label = "Explore", Enabled = true },
            new AgentAction { Id = CharacterLabActionIds.Pause, Label = "Pause", Enabled = true },
            new AgentAction { Id = CharacterLabActionIds.Resume, Label = "Resume", Enabled = true },
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
                    CharacterLabActionIds.SkinStats => DoSkinStats(),
                    CharacterLabActionIds.BoneCoverage => DoBoneCoverage(),
                    CharacterLabActionIds.ListPhases => DoListPhases(),
                    CharacterLabActionIds.SamplePhase => DoSample(command),
                    CharacterLabActionIds.SetPhaseTime => DoSeek(command),
                    CharacterLabActionIds.MeasureVertexDelta => DoMeasure(command),
                    CharacterLabActionIds.MeasureBoneTravel => DoBoneTravel(command),
                    CharacterLabActionIds.SampleHolds => DoSampleHolds(command),
                    CharacterLabActionIds.Explore => DoExplore(),
                    CharacterLabActionIds.Pause => DoPause(true),
                    CharacterLabActionIds.Resume => DoPause(false),
                    _ => Fail(id, $"Unknown action '{id}'."),
                };
            }
            catch (Exception ex)
            {
                return Fail(id, ex.Message);
            }
        }
    }

    AgentCommandResult DoSkinStats()
    {
        var s = _driver.SkinStats();
        var msg = $"source={s.Source}; clip={_driver.ActiveClipId}; bones={(int)Novolis.Simulation.Humanoid.HumanoidBone.Count - 1}; height={s.HeightMeters:0.###}";
        RaiseChanged("skinstats");
        return Ok(CharacterLabActionIds.SkinStats, msg);
    }

    AgentCommandResult DoBoneCoverage()
    {
        var sb = new StringBuilder();
        foreach (var (bone, n) in _driver.BoneCoverage())
            sb.Append(CultureInfo.InvariantCulture, $"{bone}={n}; ");
        RaiseChanged("bonecoverage");
        return Ok(CharacterLabActionIds.BoneCoverage, sb.ToString().Trim());
    }

    AgentCommandResult DoListPhases()
    {
        var sb = new StringBuilder();
        foreach (var clip in _driver.Clips)
            sb.Append(CultureInfo.InvariantCulture, $"clip:{clip.Id}={clip.Label}({clip.Source}); ");
        foreach (var (id, time, label) in DrillClips.Phases)
            sb.Append(CultureInfo.InvariantCulture, $"{id}@{time:0.###}={label}; ");
        RaiseChanged("listphases");
        return Ok(CharacterLabActionIds.ListPhases, sb.ToString().Trim());
    }

    AgentCommandResult DoSample(AgentCommand command)
    {
        ApplySeekParams(command);
        var s = _driver.SamplePose();
        var msg = string.Create(CultureInfo.InvariantCulture,
            $"phase={s.Phase}; time={s.Time:0.###}; clip={_driver.ActiveClipId}; source={_driver.SkinSource}; hips=({s.Hips.X:0.###},{s.Hips.Y:0.###},{s.Hips.Z:0.###}); head=({s.Head.X:0.###},{s.Head.Y:0.###},{s.Head.Z:0.###}); rHand=({s.RightHand.X:0.###},{s.RightHand.Y:0.###},{s.RightHand.Z:0.###}); lHand=({s.LeftHand.X:0.###},{s.LeftHand.Y:0.###},{s.LeftHand.Z:0.###}); rFoot=({s.RightFoot.X:0.###},{s.RightFoot.Y:0.###},{s.RightFoot.Z:0.###}); rifleTip=({s.RifleTip.X:0.###},{s.RifleTip.Y:0.###},{s.RifleTip.Z:0.###})");
        RaiseChanged("samplephase");
        return Ok(CharacterLabActionIds.SamplePhase, msg);
    }

    AgentCommandResult DoSeek(AgentCommand command)
    {
        ApplySeekParams(command);
        RaiseChanged("setphasetime");
        return Ok(CharacterLabActionIds.SetPhaseTime,
            $"clip={_driver.ActiveClipId}; phase={_driver.Phase}; time={_driver.TimeSeconds:0.###}");
    }

    AgentCommandResult DoMeasure(AgentCommand command)
    {
        ResolvePair(command, out var ta, out var tb);
        var d = _driver.MeasureVertexDelta(ta, tb);
        var msg = string.Create(CultureInfo.InvariantCulture,
            $"A={d.PhaseA}@{d.TimeA:0.###}; B={d.PhaseB}@{d.TimeB:0.###}; max={d.MaxDelta:0.####}; mean={d.MeanDelta:0.####}; upperMax={d.UpperBodyMaxDelta:0.####}; lowerMean={d.LowerBodyMeanDelta:0.####}");
        RaiseChanged("measurevertexdelta");
        return Ok(CharacterLabActionIds.MeasureVertexDelta, msg);
    }

    AgentCommandResult DoBoneTravel(AgentCommand command)
    {
        ResolvePair(command, out var ta, out var tb);
        var t = _driver.MeasureBoneTravel(ta, tb);
        var msg = string.Create(CultureInfo.InvariantCulture,
            $"A={t.PhaseA}@{t.TimeA:0.###}; B={t.PhaseB}@{t.TimeB:0.###}; head={t.Head:0.####}; rHand={t.RightHand:0.####}; lHand={t.LeftHand:0.####}; rFoot={t.RightFoot:0.####}; lFoot={t.LeftFoot:0.####}; hips={t.Hips:0.####}; spine2={t.Spine2:0.####}");
        RaiseChanged("measurebonetravel");
        return Ok(CharacterLabActionIds.MeasureBoneTravel, msg);
    }

    AgentCommandResult DoSampleHolds(AgentCommand command)
    {
        ApplySeekParams(command);
        var h = _driver.SampleHolds();
        var msg = string.Create(CultureInfo.InvariantCulture,
            $"phase={h.Phase}; time={h.Time:0.###}; clip={_driver.ActiveClipId}; source={_driver.SkinSource}; primary=({h.PrimaryHold.X:0.###},{h.PrimaryHold.Y:0.###},{h.PrimaryHold.Z:0.###}); secondary=({h.SecondaryHold.X:0.###},{h.SecondaryHold.Y:0.###},{h.SecondaryHold.Z:0.###}); rErr={h.RightHandError:0.####}; lErr={h.LeftHandError:0.####}");
        RaiseChanged("sampleholds");
        return Ok(CharacterLabActionIds.SampleHolds, msg);
    }

    AgentCommandResult DoExplore()
    {
        _driver.Paused = true;
        var sb = new StringBuilder();
        sb.AppendLine(DoSkinStats().Message);
        sb.AppendLine(DoListPhases().Message);

        // Prefer first CMU BVH clip for mocap travel + hold gates.
        var bvh = _driver.Clips.FirstOrDefault(c => c.Source == "cmu-bvh");
        if (bvh.Id is not null)
        {
            _driver.SelectClip(bvh.Id);
            _driver.HoldMode = true;
            var mid = _driver.DurationSeconds * 0.5f;
            var early = _driver.DurationSeconds * 0.1f;
            sb.AppendLine(DoBoneTravel(new AgentCommand
            {
                ActionId = CharacterLabActionIds.MeasureBoneTravel,
                Params = { ["timeA"] = early.ToString(CultureInfo.InvariantCulture), ["timeB"] = mid.ToString(CultureInfo.InvariantCulture) },
            }).Message);
            _driver.Seek(mid);
            sb.AppendLine(DoSampleHolds(new AgentCommand { ActionId = CharacterLabActionIds.SampleHolds }).Message);
        }

        _driver.SelectClip("synthetic-drill");
        _driver.HoldMode = true;
        foreach (var (id, _, _) in DrillClips.Phases)
        {
            _driver.SeekPhase(id);
            sb.AppendLine(DoSample(new AgentCommand { ActionId = CharacterLabActionIds.SamplePhase }).Message);
            sb.AppendLine(DoSampleHolds(new AgentCommand { ActionId = CharacterLabActionIds.SampleHolds }).Message);
        }

        void Pair(string a, string b)
        {
            var cmd = new AgentCommand
            {
                ActionId = CharacterLabActionIds.MeasureBoneTravel,
                Params = { ["phaseA"] = a, ["phaseB"] = b },
            };
            sb.Append("bones ").AppendLine(DoBoneTravel(cmd).Message);
        }

        Pair("order", "present");
        Pair("order", "salute");
        RaiseChanged("explore");
        return Ok(CharacterLabActionIds.Explore, sb.ToString().TrimEnd());
    }

    AgentCommandResult DoPause(bool paused)
    {
        _driver.Paused = paused;
        RaiseChanged(paused ? "pause" : "resume");
        return Ok(paused ? CharacterLabActionIds.Pause : CharacterLabActionIds.Resume, paused ? "paused" : "resumed");
    }

    void ApplySeekParams(AgentCommand command)
    {
        var clip = command.Get("clip");
        if (!string.IsNullOrWhiteSpace(clip))
            _driver.SelectClip(clip);

        if (TryParseInvariant(command.Get("time"), out var t))
        {
            _driver.Seek((float)t);
            return;
        }

        var phase = command.Get("phase");
        if (!string.IsNullOrWhiteSpace(phase))
            _driver.SeekPhase(phase);
    }

    void ResolvePair(AgentCommand command, out float ta, out float tb)
    {
        var clip = command.Get("clip");
        if (!string.IsNullOrWhiteSpace(clip))
            _driver.SelectClip(clip);

        // Prefer invariant parse — AgentCommand.TryGetDouble uses current culture and fails on "0.5" in comma locales.
        if (TryParseInvariant(command.Get("timeA"), out var a) && TryParseInvariant(command.Get("timeB"), out var b))
        {
            ta = (float)a;
            tb = (float)b;
            return;
        }

        ta = DrillClips.TimeForPhase(command.Get("phaseA") ?? "order");
        tb = DrillClips.TimeForPhase(command.Get("phaseB") ?? "present");
    }

    static bool TryParseInvariant(string? raw, out double value)
    {
        value = 0;
        return raw is not null &&
               double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    AgentCommandResult Ok(string actionId, string message) => new()
    {
        Ok = true,
        ActionId = actionId,
        Message = message,
        Snapshot = Snapshot(),
    };

    static AgentCommandResult Fail(string actionId, string message) => new()
    {
        Ok = false,
        ActionId = actionId,
        Message = message,
        ErrorCode = "error",
    };

    void RaiseChanged(string reason) =>
        Changed?.Invoke(new AgentChangedEvent { Reason = reason, Snapshot = Snapshot() });
}
