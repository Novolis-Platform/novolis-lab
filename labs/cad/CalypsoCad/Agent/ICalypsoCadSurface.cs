using Novolis.Agent.Surface;

namespace CalypsoCad.Agent;

[AgentSurface("calypso-cad", Description = "Calypso freighter CAD profile")]
[AgentAction(CalypsoCadActionIds.Lock, Summary = "Read the hull lock", Capability = "calypso.read", SafetyClass = "read", Idempotency = "repeatable", EffectPreview = "Returns envelope, decks, compartments, and hatches from CAL-INT-GA-001.json.")]
[AgentAction(CalypsoCadActionIds.Describe, Summary = "Describe the CAD document", Capability = "calypso.read", SafetyClass = "read", Idempotency = "repeatable", EffectPreview = "Counts entities and reports missing outfit.")]
[AgentAction(CalypsoCadActionIds.Validate, Summary = "Validate the CAD document", Capability = "calypso.read", SafetyClass = "read", Idempotency = "repeatable", EffectPreview = "Runs lock checks and reports validator errors.")]
[AgentAction(CalypsoCadActionIds.Dump, Summary = "Dump one PNG", Params = "kind|plan,orbit,interior; preset?; compartment?", Capability = "calypso.read", SafetyClass = "read", Idempotency = "repeatable", EffectPreview = "Writes one headless PNG and returns its path.")]
[AgentAction(CalypsoCadActionIds.Generate, Summary = "Regenerate CAD from the lock", Capability = "calypso.mutate", SafetyClass = "mutate", ApprovalRequired = true, Idempotency = "at-most-once", EffectPreview = "Rewrites calypso.cadjson from the JSON lock.")]
[AgentAction(CalypsoCadActionIds.Outfit, Summary = "Apply outfit solids", Params = "compartment?", Capability = "calypso.mutate", SafetyClass = "mutate", ApprovalRequired = true, Idempotency = "at-most-once", EffectPreview = "Regenerates the document so the lock outfit schedule is present.")]
[AgentAction(CalypsoCadActionIds.Structure, Summary = "Apply frames, coaming, and ramp", Capability = "calypso.mutate", SafetyClass = "mutate", ApprovalRequired = true, Idempotency = "at-most-once", EffectPreview = "Regenerates frames, stringers, the aft coaming, and the ramp from the lock.")]
internal interface ICalypsoCadSurface
{
}
