using System.Numerics;

namespace ArtillerySimulator;

internal readonly record struct KristiansandMissionBoot(
    Vector3 Objective,
    float GunX,
    float GunZ,
    float TrailBearingDegrees,
    GunSite Site,
    float CaughtMeters,
    float PinnedMeters)
{
    public static KristiansandMissionBoot From(TerrainWorld terrain, IArtilleryPieceSpec piece)
    {
        var downtown = KristiansandHeightSampler.ToWorldMeters(58.1464, 7.9956);
        var objective = new Vector3(
            downtown.X,
            terrain.Field.SampleHeight(downtown.X, downtown.Y),
            downtown.Y);

        var fort = KristiansandFort.ToWorldMeters();
        var fortY = terrain.Field.SampleHeight(fort.X, fort.Y);
        var ring = SimulationUnits.MinOpeningEngagementMeters(piece);
        var site = new GunSite(
            new Vector3(fort.X, fortY, fort.Y),
            openingElevationDegrees: 35f,
            columnLeadMeters: ring);

        return new KristiansandMissionBoot(
            objective,
            site.Position.X,
            site.Position.Z,
            OpeningFirePlan.BearingDegrees(site.Position, objective),
            site,
            piece.Warhead.CaughtMeters,
            piece.Warhead.PinnedMeters);
    }
}
