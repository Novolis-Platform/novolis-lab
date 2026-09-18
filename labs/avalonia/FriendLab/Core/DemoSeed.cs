namespace FriendLab.Core;

/// <summary>
/// Harbor District demo cluster — overlaps engineered so Alex matches Blair/Drew/Fran,
/// misses Casey (interest), and misses Eden (distance).
/// </summary>
internal static class DemoSeed
{
    // Roughly Oslo Frogner / Majorstuen area
    public const double OriginLat = 59.9225;
    public const double OriginLon = 10.7089;

    public static IReadOnlyList<FriendProfile> CreateHarborDistrict()
    {
        return
        [
            Make("alex", "Alex Rivera", "#2f6f5e", 0, 0, 4,
                "Movies", "Reading", "Hiking", "Coffee", "Museums"),
            Make("blair", "Blair Chen", "#c45c26", 0.8, 0.4, 5,
                "Movies", "Hiking", "Coffee", "Cycling", "Photography"),
            Make("casey", "Casey Novak", "#3b5b8c", -0.5, 0.6, 6,
                "Knitting", "Reading", "Gardening", "Baking", "Birdwatching"),
            Make("drew", "Drew Okonkwo", "#8a4f7d", 1.2, -0.3, 5,
                "Movies", "Reading", "Hiking", "Board Games", "Theater"),
            Make("eden", "Eden Walsh", "#6b7c3a", 40, 10, 8,
                "Movies", "Reading", "Hiking", "Coffee", "Museums"),
            Make("fran", "Fran Sato", "#b8860b", -0.9, -0.7, 3,
                "Movies", "Knitting", "Hiking", "Coffee", "Music"),
        ];
    }

    static FriendProfile Make(
        string id,
        string name,
        string accent,
        double eastKm,
        double northKm,
        double radiusKm,
        params string[] interests)
    {
        // ~111 km per degree latitude; longitude scaled by cos(lat)
        var dLat = northKm / 111.0;
        var dLon = eastKm / (111.0 * Math.Cos(OriginLat * Math.PI / 180.0));
        var profile = new FriendProfile
        {
            Id = id,
            DisplayName = name,
            AccentHex = accent,
            Latitude = OriginLat + dLat,
            Longitude = OriginLon + dLon,
            RadiusKm = radiusKm,
        };
        foreach (var interest in interests)
            profile.Interests.Add(interest);
        return profile;
    }
}
