namespace Novolis.Lab.SpaceCombat;

/// <summary>Committed display names keyed by opaque content ids (and role fallbacks).</summary>
public static class FictionTable
{
    private static readonly Dictionary<string, string> ByRole = new(StringComparer.OrdinalIgnoreCase)
    {
        ["freighter"] = "Otana",
        ["fighter"] = "X-wing",
        ["hostile"] = "TIE Fighter",
    };

    private static readonly Dictionary<int, (string Title, string Brief)> Missions = new()
    {
        [0] = ("Family Run",
            "Get the family transport clear of the Imperial ambush, then launch your X-wing from the bay."),
        [1] = ("Hot Cargo",
            "Hold course under interceptor fire. When the bay clears, scramble and protect the Otana."),
        [2] = ("Bay Launch",
            "Survive the opening wave aboard the transport, transfer to your fighter, and clear the sky."),
    };

    public static string CraftName(string? roleOrId)
    {
        if (roleOrId is null)
            return "Craft";
        if (ByRole.TryGetValue(roleOrId, out var name))
            return name;
        return "Craft";
    }

    public static (string Title, string Brief) MissionCopy(int unlockIndex) =>
        Missions.TryGetValue(unlockIndex, out var m) ? m : ($"Mission {unlockIndex + 1}", "Complete dual-role objectives.");
}
