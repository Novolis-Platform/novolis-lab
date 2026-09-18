using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace CharacterLab.Agent;

/// <summary>Attributed contract for CharacterLab drill / skin exploration.</summary>
[AgentSurface("characterlab",
    HttpPort = 18795,
    TcpPort = 18796,
    EnableEnv = "NOVOLIS_CHARACTER_SESSION",
    MarkerPrefix = "novolis-character-session",
    Description = "CharacterLab mocap wire — CMU BVH clips, hold locks, seek/scrub.")]
[AgentAction("skinstats", Summary = "Clip source + bone count")]
[AgentAction("bonecoverage", Summary = "Primary vertex counts per HumanoidBone (sorted)")]
[AgentAction("listphases", Summary = "Mocap clips + synthetic drill phases")]
[AgentAction("samplephase", Summary = "Sample bone tips + rifle", Params = "clip?; phase?; time?")]
[AgentAction("setphasetime", Summary = "Seek mocap/synthetic clock", Params = "clip?; time?; phase|order,present,salute,recover")]
[AgentAction("measurevertexdelta", Summary = "Max/mean tip Δ between times/phases", Params = "clip?; phaseA?; phaseB?; timeA?; timeB?")]
[AgentAction("measurebonetravel", Summary = "Bone tip travel", Params = "clip?; phaseA?; phaseB?; timeA?; timeB?")]
[AgentAction("sampleholds", Summary = "Hold points + hand lock errors", Params = "clip?; phase?; time?")]
[AgentAction("explore", Summary = "BVH travel + holds + synthetic phases")]
[AgentAction("pause", Summary = "Freeze playback clock")]
[AgentAction("resume", Summary = "Resume playback clock")]
public interface ICharacterLabSession : IAgentHost;

public static class CharacterLabSessionContract
{
    public static AgentSurfaceDefinition Definition { get; } = AgentSurfaceDefinition.From<ICharacterLabSession>();
}

public static class CharacterLabActionIds
{
    public const string SkinStats = "skinstats";
    public const string BoneCoverage = "bonecoverage";
    public const string ListPhases = "listphases";
    public const string SamplePhase = "samplephase";
    public const string SetPhaseTime = "setphasetime";
    public const string MeasureVertexDelta = "measurevertexdelta";
    public const string MeasureBoneTravel = "measurebonetravel";
    public const string SampleHolds = "sampleholds";
    public const string Explore = "explore";
    public const string Pause = "pause";
    public const string Resume = "resume";
}
