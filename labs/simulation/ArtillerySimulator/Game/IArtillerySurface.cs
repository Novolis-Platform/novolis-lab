using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace ArtillerySimulator.Game;

/// <summary>Live artillery window. Agents see and command this process; they do not drive the desktop.</summary>
[AgentSurface(
    "artillery",
    HttpPort = 18770,
    TcpPort = 18771,
    EnableEnv = "NOVOLIS_ARTILLERY",
    MarkerPrefix = "novolis-artillery",
    Description = "Educational 155 mm range. Snapshot the gun, then aim and fire.")]
[AgentAction("artillery.aim", Summary = "Set barrel elevation and azimuth in degrees.", Params = "elevation; azimuth", Capability = "artillery.play", SafetyClass = "mutate", EffectPreview = "Moves the barrel. The next shot uses this aim.")]
[AgentAction("artillery.charge", Summary = "Select reduced, standard, or max charge (0, 1, or 2).", Params = "index", Capability = "artillery.play", SafetyClass = "mutate", EffectPreview = "Changes muzzle velocity for the next shot.")]
[AgentAction("artillery.drag", Summary = "Turn aerodynamic drag on or off.", Params = "enabled", Capability = "artillery.play", SafetyClass = "mutate", EffectPreview = "The next shot uses drag or vacuum.")]
[AgentAction("artillery.fire", Summary = "Shoot and wait until the round lands.", Capability = "artillery.play", SafetyClass = "mutate", EffectPreview = "Integrates the shot against the range until it lands.")]
[AgentAction("artillery.pace", Summary = "Set flight pace 0..3 (about 1x, 3x, 8x, 24x).", Params = "index", Capability = "artillery.play", SafetyClass = "mutate", EffectPreview = "Changes how fast the visible flight advances.")]
[AgentAction("artillery.camera", Summary = "Choose follow or free camera.", Params = "mode", Capability = "artillery.play", SafetyClass = "mutate", EffectPreview = "Follow rides the arc. Free lets the player fly.")]
internal interface IArtillerySurface : IAgentHost;
