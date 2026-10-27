using Novolis.Ship.Structure;

namespace CalypsoCad.Generation;

internal static class FrameWebPlateRegistry
{
    internal sealed record Entry(
        FabCutProfile Profile,
        float ZStem,
        float ThicknessM,
        string DrawingRef);

    private static readonly Dictionary<string, Entry> Profiles = new(StringComparer.OrdinalIgnoreCase);

    public static void Clear() => Profiles.Clear();

    public static void Register(string partId, Entry entry) => Profiles[partId] = entry;

    public static IReadOnlyDictionary<string, Entry> All => Profiles;

    public static bool TryGet(string partId, out Entry? entry) =>
        Profiles.TryGetValue(partId, out entry);
}
