using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Novolis.Cad.Primitives;
using CalypsoCad.Structure;

namespace CalypsoCad.Blueprints;

/// <summary>
/// One-model structure query: sandwich plates and BH stations from <c>calypso.cadjson</c>.
/// Blueprints project these entities — they do not invent LOA plate bands.
/// </summary>
internal static class CadJsonStructureQuery
{
    private static readonly Regex SandwichName = new(
        @"^deck-sandwich-dk(m1|-1|0|1)-",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public sealed record SandwichPanel(
        int DeckNum,
        float PlateTop,
        float TPlate,
        float Z0,
        float Z1,
        float HalfY);

    public sealed record BhStation(string Id, float ZStem);

    public static string ResolveCadJsonPath(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return explicitPath!;

        var def = Path.Combine(CalypsoLockGenerator.DefaultOutputDirectory, "calypso.cadjson");
        if (File.Exists(def))
            return def;

        throw new FileNotFoundException(
            "calypso.cadjson missing — run generate before blueprints.", def);
    }

    public static CadDocument Load(string? cadJsonPath = null)
    {
        var path = ResolveCadJsonPath(cadJsonPath);
        return JsonSerializer.Deserialize<CadDocument>(File.ReadAllText(path), CadJson.Options)
               ?? throw new InvalidOperationException($"Failed to deserialize {path}");
    }

    public static IReadOnlyList<SandwichPanel> SandwichPanels(CadDocument doc, float loa)
    {
        var list = new List<SandwichPanel>();
        foreach (var e in doc.Entities ?? [])
        {
            if (e.Name is null || !SandwichName.IsMatch(e.Name))
                continue;
            // Prefer face-top (or legacy single slab); skip ribs / peri / face-bot duplicates.
            var n = e.Name;
            if (n.Contains("-rib-", StringComparison.OrdinalIgnoreCase)
                || n.Contains("-peri-", StringComparison.OrdinalIgnoreCase)
                || n.Contains("-core", StringComparison.OrdinalIgnoreCase)
                || n.Contains("-face-bot", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!TryBox(e, out var center, out var half))
                continue;

            var deckNum = ParseDeckNum(e.Name);
            float plateTop;
            float tPlate;
            if (e.Properties is not null
                && e.Properties.TryGetValue("deckUp", out var duEl)
                && duEl.ValueKind == JsonValueKind.Number)
            {
                plateTop = (float)duEl.GetDouble();
                // face-top half-height is thin; recover stack thickness from name convention (~tPlate).
                tPlate = n.Contains("-face-top", StringComparison.OrdinalIgnoreCase)
                    ? MathF.Max(0.08f, MathF.Abs(half.Y) * 2f / 0.22f)
                    : MathF.Abs(half.Y) * 2f;
            }
            else
            {
                var upCenter = center.Y;
                var halfUp = MathF.Abs(half.Y);
                plateTop = upCenter + halfUp;
                tPlate = halfUp * 2f;
            }

            var zStem = loa * 0.5f - center.Z;
            var halfZ = MathF.Abs(half.Z);
            list.Add(new SandwichPanel(
                deckNum,
                plateTop,
                tPlate,
                zStem - halfZ,
                zStem + halfZ,
                MathF.Abs(half.X)));
        }

        return list;
    }

    public static IReadOnlyList<(float Z0, float Z1)> MergedPlateSpans(
        IReadOnlyList<SandwichPanel> panels, int deckNum, float mergeGap = 0.35f)
    {
        var segs = panels
            .Where(p => p.DeckNum == deckNum)
            .Select(p => (p.Z0, p.Z1))
            .OrderBy(s => s.Z0)
            .ToList();
        if (segs.Count == 0)
            return [];

        var merged = new List<(float Z0, float Z1)> { segs[0] };
        for (var i = 1; i < segs.Count; i++)
        {
            var last = merged[^1];
            if (segs[i].Z0 <= last.Z1 + mergeGap)
                merged[^1] = (last.Z0, MathF.Max(last.Z1, segs[i].Z1));
            else
                merged.Add(segs[i]);
        }

        return merged;
    }

    public static float? PlateTopForDeck(IReadOnlyList<SandwichPanel> panels, int deckNum)
    {
        var match = panels.Where(p => p.DeckNum == deckNum).ToList();
        if (match.Count == 0)
            return null;
        return match.Average(p => p.PlateTop);
    }

    public static float? TPlateForDeck(IReadOnlyList<SandwichPanel> panels, int deckNum)
    {
        var match = panels.Where(p => p.DeckNum == deckNum).ToList();
        if (match.Count == 0)
            return null;
        return match.Average(p => p.TPlate);
    }

    public static IReadOnlyList<BhStation> BulkheadStations(CadDocument doc, float loa)
    {
        var byId = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in doc.Entities ?? [])
        {
            if (e.Name is null
                || !e.Name.StartsWith("bh-", StringComparison.OrdinalIgnoreCase)
                || !TryBox(e, out var center, out _))
                continue;

            // bh-ENG-AFT-plate / bh-ENG-AFT-web-solid-0 → ENG-AFT
            var rest = e.Name["bh-".Length..];
            var cut = rest.IndexOf("-plate", StringComparison.OrdinalIgnoreCase);
            if (cut < 0)
                cut = rest.IndexOf("-web", StringComparison.OrdinalIgnoreCase);
            if (cut < 0)
                cut = rest.IndexOf("-flange", StringComparison.OrdinalIgnoreCase);
            var id = cut > 0 ? rest[..cut] : rest.Split('-')[0];
            var zStem = loa * 0.5f - center.Z;
            if (!byId.TryGetValue(id, out var existing) || MathF.Abs(zStem) < MathF.Abs(existing))
                byId[id] = zStem;
        }

        return byId
            .Select(kv => new BhStation(kv.Key, kv.Value))
            .OrderBy(b => b.ZStem)
            .ToList();
    }

    public static int CountHoldWebSolids(CadDocument doc, float loa, float holdFore = 49.3f, float holdAft = 67.5f)
    {
        var n = 0;
        foreach (var e in doc.Entities ?? [])
        {
            if (e.Name is null
                || !e.Name.StartsWith("frame-F", StringComparison.OrdinalIgnoreCase)
                || !e.Name.Contains("-web-solid-", StringComparison.OrdinalIgnoreCase)
                || !TryBox(e, out var center, out _))
                continue;
            var zStem = loa * 0.5f - center.Z;
            if (zStem > holdFore && zStem < holdAft)
                n++;
        }

        return n;
    }

    private static bool TryBox(CadEntity e, out System.Numerics.Vector3 center, out System.Numerics.Vector3 half)
    {
        if (e.Center is { Length: >= 3 } c && e.HalfExtents is { Length: >= 3 } h)
        {
            center = new System.Numerics.Vector3(c[0], c[1], c[2]);
            half = new System.Numerics.Vector3(h[0], h[1], h[2]);
            return true;
        }

        // Generator boxes stamp Points[0]=center, Points[1]=halfExtents.
        if (e.Points is { Count: >= 2 } pts && pts[0].Length >= 3 && pts[1].Length >= 3)
        {
            center = new System.Numerics.Vector3(pts[0][0], pts[0][1], pts[0][2]);
            half = new System.Numerics.Vector3(pts[1][0], pts[1][1], pts[1][2]);
            return true;
        }

        center = default;
        half = default;
        return false;
    }

    private static int ParseDeckNum(string name)
    {
        var m = SandwichName.Match(name);
        if (!m.Success)
            return 0;
        var tag = m.Groups[1].Value;
        if (tag.Equals("m1", StringComparison.OrdinalIgnoreCase) || tag == "-1")
            return -1;
        return int.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }
}
