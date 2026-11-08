using System.Globalization;
using Novolis.Agent.Core;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

internal sealed class ArtilleryImpactEventPublisher(
    IPlayQuery play,
    ArtilleryAgentHostState state,
    ArtilleryAgentSnapshotBuilder snapshots)
{
    private readonly List<TrackedImpact> _awaitingImpact = [];

    public void TrackFire(BallisticTrajectoryRunner round, string? correlationId) =>
        _awaitingImpact.Add(new TrackedImpact(round, correlationId));

    public void CollectImpacts(List<AgentActionResultEvent> events)
    {
        for (var i = _awaitingImpact.Count - 1; i >= 0; i--)
        {
            var tracked = _awaitingImpact[i];
            if (tracked.Round.Phase == BallisticTrajectoryPhase.InFlight)
                continue;

            _awaitingImpact.RemoveAt(i);
            var impact = tracked.Round.Impact;
            var message = impact is { } hit
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"Impact {hit.HorizontalRangeMeters / 1000f:F2} km after {hit.TimeSeconds:F1} s ({hit.Reason}), {hit.ImpactSpeedMps:F0} m/s. {play.Mission?.ResultPhrase()}")
                : "The shot did not impact.";

            state.Revision++;
            var snapshot = snapshots.Capture();
            events.Add(new AgentActionResultEvent
            {
                Sequence = ++state.Sequence,
                ActionId = "artillery.impact",
                Ok = impact is not null,
                Message = message,
                CorrelationId = tracked.CorrelationId,
                ResourceId = ArtilleryAgentHostState.GunResource,
                Severity = AgentSeverity.Info,
                Snapshot = snapshot,
            });
        }
    }

    private readonly record struct TrackedImpact(BallisticTrajectoryRunner Round, string? CorrelationId);
}
