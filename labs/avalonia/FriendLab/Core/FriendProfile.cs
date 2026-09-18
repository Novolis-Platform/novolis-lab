namespace FriendLab.Core;

internal sealed class FriendProfile
{
    public required string Id { get; init; }
    public required string DisplayName { get; set; }
    public required string AccentHex { get; init; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusKm { get; set; } = 5;
    public HashSet<string> Interests { get; } = new(StringComparer.OrdinalIgnoreCase);

    public FriendProfile CloneSnapshot()
    {
        var copy = new FriendProfile
        {
            Id = Id,
            DisplayName = DisplayName,
            AccentHex = AccentHex,
            Latitude = Latitude,
            Longitude = Longitude,
            RadiusKm = RadiusKm,
        };
        foreach (var interest in Interests)
            copy.Interests.Add(interest);
        return copy;
    }
}
