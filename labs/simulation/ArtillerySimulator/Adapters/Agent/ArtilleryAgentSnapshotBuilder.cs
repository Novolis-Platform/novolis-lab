using System.Globalization;
using Novolis.Agent.Core;

namespace ArtillerySimulator;

internal sealed class ArtilleryAgentSnapshotBuilder(
    IPlayQuery play,
    ArtilleryAgentHostState state,
    ArtilleryAgentActionCatalog catalog,
    ArtilleryAgentEntityFactory entities)
{
    public AgentSnapshot Capture()
    {
        var gun = play.Gun;
        var shot = play.Shot;
        var terrain = play.Terrain;
        var impact = shot.Impact;
        var lines = BuildStatusLines();
        var summary = BuildSummary();

        return new AgentSnapshot
        {
            Sequence = ++state.Sequence,
            HubId = ArtilleryAgentHostState.GunResource,
            HubName = "155 mm gun",
            DocumentName = $"{ProductIdentity.Title} — {ProductIdentity.Subtitle}",
            SeedHash = play.Session.SeedHash,
            PauseReason = shot.Phase.ToString(),
            StateRevision = state.Revision,
            MetadataVersion = AgentMetadata.Version,
            StatusLines = lines,
            MapVisible = true,
            MapX = impact?.Position.X ?? terrain.GunBaseline.X,
            MapY = impact?.Position.Z ?? terrain.GunBaseline.Z,
            Actions = catalog.Catalog(),
            Receipt = state.Receipt,
            LastAction = state.Last,
            Observations =
            [
                new AgentObservation
                {
                    ResourceId = ArtilleryAgentHostState.GunResource,
                    StateRevision = state.Revision,
                    Summary = summary,
                    AvailableActionIds = catalog.Catalog().Where(action => action.Enabled).Select(action => action.Id).ToArray(),
                },
            ],
            Entities = entities.BuildEntities(),
        };
    }

    public AgentChangedEvent? NoteChange()
    {
        var gun = play.Gun;
        var shot = play.Shot;
        var mission = play.Mission;
        var crew = play.Crew;
        var defense = mission is null
            ? ""
            : string.Create(
                CultureInfo.InvariantCulture,
                $"|{mission.Remaining}|{(int)(mission.NearestMeters / 25f)}|{mission.ObjectiveLost}|{mission.Held}|{mission.PostureToken()}|{crew.Token(gun)}|{(int)MathF.Ceiling(crew.BusySeconds(gun))}");
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{shot.Phase}|{gun.ElevationDegrees:F0}|{gun.AzimuthDegrees:F0}|{crew.OrderedElevation:F0}|{crew.OrderedAzimuth:F0}|{crew.OrderedCharge}|{play.CameraModeName}|{play.PaceText}|{shot.Impact?.HorizontalRangeMeters:F0}{defense}");
        if (key == state.Seen)
            return null;

        state.Seen = key;
        return new AgentChangedEvent
        {
            Sequence = ++state.Sequence,
            Reason = shot.Phase.ToString(),
            ResourceId = ArtilleryAgentHostState.GunResource,
            Severity = AgentSeverity.Info,
            Snapshot = Capture(),
        };
    }

    private Dictionary<string, string> BuildStatusLines()
    {
        var gun = play.Gun;
        var shot = play.Shot;
        var terrain = play.Terrain;
        var impact = shot.Impact;
        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["elevation"] = gun.ElevationDegrees.ToString("F1", CultureInfo.InvariantCulture),
            ["azimuth"] = gun.AzimuthDegrees.ToString("F1", CultureInfo.InvariantCulture),
            ["charge"] = gun.ChargeLabel,
            ["muzzleMps"] = gun.MuzzleSpeedMps.ToString("F0", CultureInfo.InvariantCulture),
            ["phase"] = shot.Phase.ToString(),
            ["camera"] = play.CameraModeName,
            ["pace"] = play.PaceText,
            ["orderedElevation"] = play.Crew.OrderedElevation.ToString("F1", CultureInfo.InvariantCulture),
            ["orderedAzimuth"] = play.Crew.OrderedAzimuth.ToString("F1", CultureInfo.InvariantCulture),
            ["crew"] = play.Crew.Token(gun),
            ["reload"] = play.Crew.Ready(gun) ? "ready" : MathF.Ceiling(play.Crew.BusySeconds(gun)).ToString("F0", CultureInfo.InvariantCulture),
            ["predictedMeters"] = play.PredictedRangeMeters.ToString("F0", CultureInfo.InvariantCulture),
            ["gunX"] = terrain.GunBaseline.X.ToString("F0", CultureInfo.InvariantCulture),
            ["gunZ"] = terrain.GunBaseline.Z.ToString("F0", CultureInfo.InvariantCulture),
            ["terrain"] = "Kristiansand",
            ["latitude"] = KristiansandHeightSampler.MeanLatitudeDegrees.ToString("F2", CultureInfo.InvariantCulture),
            ["windMps"] = play.Atmosphere.WindSpeedMetersPerSecond.ToString("F1", CultureInfo.InvariantCulture),
            ["windFrom"] = play.Atmosphere.WindFromDegrees.ToString("F0", CultureInfo.InvariantCulture),
            ["windToward"] = play.Atmosphere.WindTowardDegrees.ToString("F0", CultureInfo.InvariantCulture),
            ["tempC"] = (play.Atmosphere.ReferenceTemperatureKelvin - 273.15).ToString("F1", CultureInfo.InvariantCulture),
            ["rh"] = (play.Atmosphere.RelativeHumidity * 100.0).ToString("F0", CultureInfo.InvariantCulture),
            ["qnh"] = (play.Atmosphere.ReferencePressurePa / 100.0).ToString("F0", CultureInfo.InvariantCulture),
            ["metAge"] = play.Atmosphere.BattleAgeSeconds.ToString("F0", CultureInfo.InvariantCulture),
            ["level"] = play.Level.Id,
            ["levelName"] = play.Level.DisplayName,
            ["levelSite"] = play.Level.SiteName,
            ["piece"] = gun.Spec.Id,
            ["pieceName"] = gun.Spec.ShortName,
            ["peElevMils"] = gun.Spec.Errors.ElevationPeMils.ToString("F2", CultureInfo.InvariantCulture),
            ["peDeflMils"] = gun.Spec.Errors.DeflectionPeMils.ToString("F2", CultureInfo.InvariantCulture),
            ["peMvFrac"] = gun.Spec.Errors.MuzzleVelocityPeFraction.ToString("F4", CultureInfo.InvariantCulture),
            ["heCaughtM"] = gun.Spec.Warhead.CaughtMeters.ToString("F0", CultureInfo.InvariantCulture),
            ["hePinnedM"] = gun.Spec.Warhead.PinnedMeters.ToString("F0", CultureInfo.InvariantCulture),
            ["heAirburstM"] = gun.Spec.Warhead.AirburstHeightMeters.ToString("F0", CultureInfo.InvariantCulture),
            ["seed"] = play.Session.SeedHash,
            ["score"] = play.ScoreSnapshot.Points.ToString(CultureInfo.InvariantCulture),
            ["highScore"] = play.ScoreSnapshot.HighScore.ToString(CultureInfo.InvariantCulture),
            ["scoreTime"] = play.ScoreSnapshot.TimeText,
            ["shots"] = play.ScoreSnapshot.Shots.ToString(CultureInfo.InvariantCulture),
            ["kills"] = play.ScoreSnapshot.Kills.ToString(CultureInfo.InvariantCulture),
            ["pins"] = play.ScoreSnapshot.Pins.ToString(CultureInfo.InvariantCulture),
            ["scoreFinal"] = play.ScoreSnapshot.Finalized ? "yes" : "no",
        };
        if (play.Delivery.LastFire is { } last)
        {
            lines["fireElevResMils"] = last.ElevationResidualMils.ToString("F2", CultureInfo.InvariantCulture);
            lines["fireDeflResMils"] = last.DeflectionResidualMils.ToString("F2", CultureInfo.InvariantCulture);
            lines["fireMvResFrac"] = last.MuzzleResidualFraction.ToString("F4", CultureInfo.InvariantCulture);
        }
        if (play.Mission is { } mission)
        {
            lines["deflection"] = gun.DeflectionDegrees.ToString("F1", CultureInfo.InvariantCulture);
            lines["trail"] = gun.CenterAzimuthDegrees.ToString("F1", CultureInfo.InvariantCulture);
            lines["objectiveX"] = mission.Objective.X.ToString("F0", CultureInfo.InvariantCulture);
            lines["objectiveZ"] = mission.Objective.Z.ToString("F0", CultureInfo.InvariantCulture);
            lines["enemies"] = mission.Remaining.ToString(CultureInfo.InvariantCulture);
            lines["nearestMeters"] = mission.NearestMeters.ToString("F0", CultureInfo.InvariantCulture);
            lines["defenders"] = mission.DefendersAlive.ToString(CultureInfo.InvariantCulture);
            lines["fratricide"] = mission.FratricideLoss ? "yes" : "no";
            lines["outcome"] = mission.Outcome.ToString();
            lines["battleSeconds"] = mission.BattleElapsedSeconds.ToString("F1", CultureInfo.InvariantCulture);
            lines["defense"] = mission.Outcome switch
            {
                MissionOutcome.Held => "held",
                MissionOutcome.Fratricide or MissionOutcome.ObjectiveLost => "lost",
                _ => "open",
            };
        }
        if (impact is { } hit)
        {
            lines["rangeMeters"] = hit.HorizontalRangeMeters.ToString("F0", CultureInfo.InvariantCulture);
            lines["flightSeconds"] = hit.TimeSeconds.ToString("F1", CultureInfo.InvariantCulture);
            lines["impact"] = hit.Reason.ToString();
        }

        return lines;
    }

    private string BuildSummary()
    {
        var gun = play.Gun;
        var shot = play.Shot;
        var impact = shot.Impact;
        var defense = play.Mission?.Observation();
        var crewNote = play.Crew.Ready(gun) ? "" : " " + play.Crew.Phrase(gun);
        return impact is { } recorded
            ? string.Create(CultureInfo.InvariantCulture, $"{shot.Phase}: aim {gun.ElevationDegrees:F0}° {gun.ChargeLabel}, impact {recorded.HorizontalRangeMeters / 1000f:F2} km ({recorded.Reason}). {defense}{crewNote}")
            : string.Create(CultureInfo.InvariantCulture, $"{shot.Phase}: aim {play.Crew.OrderedElevation:F0}° / {play.Crew.OrderedAzimuth:F0}°, {gun.ChargeLabel}, predicted {play.PredictedRangeMeters / 1000f:F2} km. {defense}{crewNote}");
    }
}
