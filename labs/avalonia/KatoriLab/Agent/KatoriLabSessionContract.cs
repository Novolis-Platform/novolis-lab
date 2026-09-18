using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace KatoriLab.Agent;

[AgentSurface("katorilab",
    HttpPort = 18797,
    TcpPort = 18798,
    EnableEnv = "NOVOLIS_KATORI_SESSION",
    MarkerPrefix = "novolis-katori-session",
    Description = "KatoriLab ken kata — stylized TSKSR vocabulary, bokken hold locks, seek/scrub.")]
[AgentAction("listphases", Summary = "Kata phases (rei/chudan/jodan/kesagiri/gedan/recover)")]
[AgentAction("samplephase", Summary = "Sample bone tips + kissaki", Params = "phase?; time?")]
[AgentAction("setphasetime", Summary = "Seek kata clock", Params = "time?; phase|door,walk,opening,chudan,jodan,kesagiri,gedan,closing,leave")]
[AgentAction("measurevertexdelta", Summary = "Max/mean tip Δ between times/phases", Params = "phaseA?; phaseB?; timeA?; timeB?")]
[AgentAction("measurebonetravel", Summary = "Bone tip travel", Params = "phaseA?; phaseB?; timeA?; timeB?")]
[AgentAction("sampleholds", Summary = "Bokken hold points + hand lock errors", Params = "phase?; time?")]
[AgentAction("diagnose", Summary = "Kamae geometry: blade dir, reach, elbows, grip errors", Params = "phase?; time?")]
[AgentAction("explore", Summary = "Chudan→jodan travel + hold locks + diagnose")]
[AgentAction("pause", Summary = "Freeze playback clock")]
[AgentAction("resume", Summary = "Resume playback clock")]
public interface IKatoriLabSession : IAgentHost;

public static class KatoriLabSessionContract
{
    public static AgentSurfaceDefinition Definition { get; } = AgentSurfaceDefinition.From<IKatoriLabSession>();
}

public static class KatoriLabActionIds
{
    public const string ListPhases = "listphases";
    public const string SamplePhase = "samplephase";
    public const string SetPhaseTime = "setphasetime";
    public const string MeasureVertexDelta = "measurevertexdelta";
    public const string MeasureBoneTravel = "measurebonetravel";
    public const string SampleHolds = "sampleholds";
    public const string Diagnose = "diagnose";
    public const string Explore = "explore";
    public const string Pause = "pause";
    public const string Resume = "resume";
}
