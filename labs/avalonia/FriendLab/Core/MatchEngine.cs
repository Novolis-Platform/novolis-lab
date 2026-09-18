namespace FriendLab.Core;

internal static class MatchEngine
{
    public static IReadOnlyList<MatchResult> FindMatches(FriendProfile viewer, IEnumerable<FriendProfile> everyone)
    {
        var results = new List<MatchResult>();

        foreach (var candidate in everyone)
        {
            if (ReferenceEquals(candidate, viewer) || candidate.Id == viewer.Id)
                continue;

            if (viewer.Interests.Count != InterestCatalog.RequiredPicks)
                continue;
            if (candidate.Interests.Count != InterestCatalog.RequiredPicks)
                continue;

            var shared = viewer.Interests
                .Where(candidate.Interests.Contains)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (shared.Count < InterestCatalog.MinOverlap)
                continue;

            var distance = Geo.DistanceKm(
                viewer.Latitude, viewer.Longitude,
                candidate.Latitude, candidate.Longitude);

            if (distance > viewer.RadiusKm)
                continue;

            var withinTheirs = distance <= candidate.RadiusKm;
            results.Add(new MatchResult(
                candidate,
                distance,
                shared,
                withinTheirs,
                ActivitySuggestions.Suggest(shared)));
        }

        return results
            .OrderByDescending(m => m.SharedInterests.Count)
            .ThenBy(m => m.DistanceKm)
            .ToList();
    }
}
