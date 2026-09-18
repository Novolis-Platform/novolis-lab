namespace FriendLab.Core;

internal static class InterestCatalog
{
    public const int RequiredPicks = 5;
    public const int MinOverlap = 3;

    public static IReadOnlyList<string> All { get; } =
    [
        "Movies",
        "Reading",
        "Knitting",
        "Hiking",
        "Board Games",
        "Cooking",
        "Photography",
        "Cycling",
        "Music",
        "Gardening",
        "Coffee",
        "Museums",
        "Running",
        "Gaming",
        "Pottery",
        "Birdwatching",
        "Volunteering",
        "Theater",
        "Climbing",
        "Baking",
    ];
}
