namespace FriendLab.Core;

internal static class ActivitySuggestions
{
    static readonly Dictionary<string, string[]> ByInterest = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Movies"] = ["catch a matinee at the neighborhood cinema", "browse lobby posters and grab popcorn"],
        ["Reading"] = ["meet at the public library reading room", "browse the indie bookshop new arrivals table"],
        ["Knitting"] = ["join the yarn circle at the craft co-op", "swap patterns over tea at the community center"],
        ["Hiking"] = ["walk the riverside trail loop (daylight, busy path)", "meet at the trailhead cafe before a short ridge walk"],
        ["Board Games"] = ["claim a table at the game cafe open night", "try a gateway game at the library games hour"],
        ["Cooking"] = ["take a public market cooking class", "wander the farmers market and trade recipes"],
        ["Photography"] = ["golden-hour walk along the waterfront promenade", "photo walk through the botanical garden"],
        ["Cycling"] = ["easy ride on the harbor bike path", "coffee stop after a park loop"],
        ["Music"] = ["outdoor bandstand set in the park", "browse vinyl at the record shop Saturday"],
        ["Gardening"] = ["volunteer hour at the community garden", "tour the botanical conservatory"],
        ["Coffee"] = ["meet at the busy corner cafe", "compare pour-overs at the market stall"],
        ["Museums"] = ["afternoon at the city museum commons", "free gallery night at the civic arts center"],
        ["Running"] = ["parkrun-style group jog on the lit path", "stretch and chat after a short embankment run"],
        ["Gaming"] = ["local esports lounge casual night", "tabletop + light games at the youth center hall"],
        ["Pottery"] = ["drop-in studio at the makerspace", "clay demo at the craft fair"],
        ["Birdwatching"] = ["dawn watch from the wetland boardwalk", "binocular hour at the nature center"],
        ["Volunteering"] = ["park cleanup morning with the neighborhood crew", "food bank sorting shift (public hall)"],
        ["Theater"] = ["matinee at the community playhouse", "open rehearsal talkback at the civic theater"],
        ["Climbing"] = ["bouldering session at the indoor gym", "beginner belay clinic (staffed)"],
        ["Baking"] = ["bake-sale prep at the community kitchen", "pastry tasting at the weekend market"],
    };

    public static string Suggest(IReadOnlyList<string> shared)
    {
        if (shared.Count == 0)
            return "meet somewhere public and busy — park plaza or cafe";

        var key = shared[0];
        if (ByInterest.TryGetValue(key, out var options) && options.Length > 0)
        {
            var pick = options[Math.Abs(HashCode.Combine(string.Join('|', shared))) % options.Length];
            return $"{pick} (shared: {string.Join(", ", shared)})";
        }

        return $"plan something public around {string.Join(", ", shared.Take(3))}";
    }
}
