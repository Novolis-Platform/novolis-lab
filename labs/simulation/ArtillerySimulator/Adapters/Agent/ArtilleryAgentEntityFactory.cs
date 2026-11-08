using System.Globalization;
using Novolis.Agent.Core;

namespace ArtillerySimulator;

internal sealed class ArtilleryAgentEntityFactory(IPlayQuery play)
{
    public AgentEntity[] BuildEntities()
    {
        if (play.Mission is not { } mission)
            return [];

        var gun = play.Terrain.GunBaseline;
        var reports = mission.Reports(gun);
        var entities = new List<AgentEntity>(reports.Length + 12);
        foreach (var threat in reports)
        {
            var where = threat.InArc ? "in traverse" : "outside traverse";
            entities.Add(new AgentEntity
            {
                Id = string.Create(CultureInfo.InvariantCulture, $"threat-{threat.Id}"),
                Kind = AgentEntity.Threat,
                Name = threat.Road,
                State = threat.Alive ? threat.Posture : "stopped",
                X = threat.X,
                Z = threat.Z,
                Summary = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{threat.Posture}, {where} on {threat.Road}, {threat.MetersFromGun / 1000f:F2} km from the gun at {threat.BearingDegrees:F0}°, {threat.MetersToCrossing / 1000f:F2} km from the crossing"),
            });
        }

        entities.Add(new AgentEntity
        {
            Id = "objective",
            Kind = AgentEntity.Objective,
            Name = "Kvadraturen",
            State = mission.ObjectiveLost ? "lost" : mission.Held ? "held" : "open",
            X = mission.Objective.X,
            Z = mission.Objective.Z,
            Summary = mission.Observation(),
        });

        foreach (var defender in mission.DefenderReports())
        {
            entities.Add(new AgentEntity
            {
                Id = string.Create(CultureInfo.InvariantCulture, $"defender-{defender.Label}"),
                Kind = "defender",
                Name = defender.Label,
                State = defender.Alive ? "holding" : "casualty",
                X = defender.X,
                Z = defender.Z,
                Summary = defender.Alive
                    ? "Friendly defender on a downtown chokepoint."
                    : "Defender out of action.",
            });
        }

        entities.AddRange(KristiansandMapFeatures.MapEntities());
        return entities.ToArray();
    }
}
