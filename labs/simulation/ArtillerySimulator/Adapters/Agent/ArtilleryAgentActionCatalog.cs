using Novolis.Agent.Core;

namespace ArtillerySimulator;

internal sealed class ArtilleryAgentActionCatalog(IPlayQuery play)
{
    public AgentAction[] Catalog()
    {
        var blocked = play.FireBlockReason();
        return
        [
            Action("artillery.aim", "Aim", "Order elevation and world azimuth. The crew lays the tube on the handwheels. Trails clamp the azimuth.", AgentActionSchema.Object(["elevation", "azimuth"], ("elevation", "number"), ("azimuth", "number")), true, null),
            Action("artillery.charge", "Charge", "Order reduced, standard, max, or low (0, 1, 2, or 3). A new zone is reseated before the next shot.", AgentActionSchema.Object(["index"], ("index", "integer")), true, null),
            Action("artillery.fire", "Fire", "Shoot when the breech is closed and the tube is on the order. Returns immediately; impact arrives as artillery.impact.", null, blocked is null, blocked),
            Action("artillery.pace", "Pace", "Realtime only — fixed battle clock.", AgentActionSchema.Object(["index"], ("index", "integer")), true, null),
            Action("artillery.camera", "Camera", "Choose shell follow or gun orbit.", AgentActionSchema.Object(["mode"], ("mode", "string")), true, null),
        ];
    }

    private static AgentAction Action(string id, string label, string summary, Dictionary<string, object?>? schema, bool enabled, string? disabledReason) =>
        new()
        {
            Id = id,
            Label = label,
            Summary = summary,
            Schema = schema,
            Enabled = enabled,
            DisabledReason = disabledReason,
            Capability = "artillery.play",
            SafetyClass = AgentSafetyClass.Mutate,
            ApprovalRequired = false,
            Idempotency = "correlation",
            MetadataVersion = AgentMetadata.Version,
        };
}
