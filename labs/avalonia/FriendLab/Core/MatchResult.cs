namespace FriendLab.Core;

internal sealed record MatchResult(
    FriendProfile Candidate,
    double DistanceKm,
    IReadOnlyList<string> SharedInterests,
    bool WithinTheirRadius,
    string SuggestedActivity);
