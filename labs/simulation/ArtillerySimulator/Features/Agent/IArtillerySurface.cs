using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace ArtillerySimulator;

/// <summary>Live artillery window. Agents see and command this process; they do not drive the desktop.</summary>
[AgentSurface(
    "artillery",
    HttpPort = 18770,
    TcpPort = 18771,
    EnableEnv = "NOVOLIS_ARTILLERY",
    MarkerPrefix = "novolis-artillery",
    Description = "K9 DOG — Howitzer defence simulation game. Levels are pluggable (default: Defense of Kristiansand). Educational sandbox (not a manufacturer product).")]
[AgentAction("artillery.aim", Summary = "Order elevation and world azimuth in degrees. The crew lays the tube on the handwheels. Trails clamp azimuth to ±22.5° around the laid center.", Params = "elevation; azimuth", Capability = "artillery.play", SafetyClass = "mutate", EffectPreview = "Orders a lay. The tube slews, and the next shot uses it when the crew is on it.")]
[AgentAction("artillery.charge", Summary = "Order reduced, standard, max, or low charge (0, 1, 2, or 3). A different zone is reseated before the next shot. Low charge keeps a steep shell shorter.", Params = "index", Capability = "artillery.play", SafetyClass = "mutate", EffectPreview = "Orders a charge zone. A change keeps the breech busy until the new zone is seated.")]
[AgentAction("artillery.fire", Summary = "Shoot when the breech is closed and the tube is on the order. The crew rams the next round during the flight. The burst puts vehicles near the impact out of action and pins the next ones.", Capability = "artillery.play", SafetyClass = "mutate", EffectPreview = "Integrates the shot. Vehicles near the impact are out of action. The crew reloads on the same clock.")]
[AgentAction("artillery.pace", Summary = "Realtime only — pace multipliers are removed; this action is a no-op.", Params = "index", Capability = "artillery.play", SafetyClass = "mutate", EffectPreview = "Confirms realtime clock.")]
[AgentAction("artillery.camera", Summary = "Choose follow or free camera.", Params = "mode", Capability = "artillery.play", SafetyClass = "mutate", EffectPreview = "Follow rides the arc. Free lets the player fly.")]
internal interface IArtillerySurface : IAgentHost;
