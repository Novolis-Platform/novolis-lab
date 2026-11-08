using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Novolis.Cad.Primitives;
using Novolis.Ship.Primitives;
using CalypsoCad.Structure;

namespace CalypsoCad.Blueprints;

/// <summary>
/// One-model drafting query: structure + volumes + loft from <c>calypso.cadjson</c> only.
/// Lock JSON is generate input — package sheets must not open a second CAD.
/// </summary>
internal static class CadJsonStructureQuery
{
    private static readonly Regex SandwichName = new(
        @"^deck-sandwich-dk(m1|-1|0|1)-",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FrameAssembly = new(
        @"^frame-F(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public const float SteelDensityKgPerM3 = 8000f;

    public sealed record SandwichPanel(
        int DeckNum,
        float PlateTop,
        float FaceThicknessM,
        float Z0,
        float Z1,
        float Y0,
        float Y1,
        string EntityName);

    public sealed record DeckSteelMass(
        int DeckNum,
        float SteelVolumeM3,
        float MassKg,
        int PartCount,
        float FaceTopAreaM2);

    public sealed record FrameStation(string FrameId, float ZStem, int PartCount);

    public sealed record FramePartBox(string Name, float Y0, float Y1, float Up0, float Up1);

    public sealed record BhStation(string Id, float ZStem);

    public sealed record DeckElevations(float M1, float D0, float D1, float TPlate, float RoomH);

    public sealed record SpaceVolume(
        string Id,
        int Deck,
        float Z0,
        float Z1,
        float Y0,
        float Y1,
        float Up0,
        float Up1,
        IReadOnlyList<(float Y, float ZStem)> PlanRing);

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

    public static float Loa(CadDocument doc) => ShipDocumentMetrics.GetLoaMeters(doc);

    public static float Beam(CadDocument doc) => ShipDocumentMetrics.GetBeamMeters(doc);

    public static float Oah(CadDocument doc) => ShipDocumentMetrics.GetHeightMeters(doc);

    public static float FrameBayM(CadDocument doc)
    {
        if (doc.Properties is not null
            && doc.Properties.TryGetValue("frameBayM", out var el)
            && el.ValueKind == JsonValueKind.Number)
            return (float)el.GetDouble();
        return 4f;
    }

    public static DeckElevations Decks(CadDocument doc)
    {
        if (doc.Properties is not null
            && doc.Properties.TryGetValue("decks", out var el)
            && el.ValueKind == JsonValueKind.Object)
        {
            float Get(string key, float fallback) =>
                el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
                    ? (float)v.GetDouble()
                    : fallback;
            return new DeckElevations(Get("m1", 0.5f), Get("d0", 4.4f), Get("d1", 8f), Get("tPlate", 0.1f), Get("roomH", 3.2f));
        }

        return new DeckElevations(0.5f, 4.4f, 8f, 0.1f, 3.2f);
    }

    public static IReadOnlyList<LockDraftGeometry.LoftStation> HullLoft(CadDocument doc)
    {
        if (doc.Properties is null || !doc.Properties.TryGetValue("hullLoft", out var hl))
            return [];
        // Wrap so LockDraftGeometry.ParseLoft can find hullLoft.stations.
        using var wrap = JsonDocument.Parse(
            JsonSerializer.Serialize(new Dictionary<string, JsonElement> { ["hullLoft"] = hl }));
        return LockDraftGeometry.ParseLoft(wrap.RootElement);
    }

    public static Dictionary<string, float> StationMap(CadDocument doc)
    {
        var map = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (doc.Properties is null || !doc.Properties.TryGetValue("stations", out var st)
            || st.ValueKind != JsonValueKind.Object)
            return map;
        foreach (var p in st.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.Number)
                map[p.Name] = (float)p.Value.GetDouble();
        }

        return map;
    }

    /// <summary>Unique space ids from CAD <c>space</c> entities (merged across decks).</summary>
    public static IReadOnlyList<SpaceVolume> Spaces(CadDocument doc)
    {
        var loa = Loa(doc);
        var byId = new Dictionary<string, SpaceVolume>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in doc.Entities ?? [])
        {
            if (e.Kind is not ("space" or "airlock") || string.IsNullOrWhiteSpace(e.Name))
                continue;
            if (e.Points is not { Count: >= 3 })
                continue;

            float y0 = float.MaxValue, y1 = float.MinValue;
            float z0 = float.MaxValue, z1 = float.MinValue;
            float u0 = float.MaxValue, u1 = float.MinValue;
            var ring = new List<(float Y, float ZStem)>();
            foreach (var p in e.Points)
            {
                if (p.Length < 3)
                    continue;
                var y = p[0];
                var up = p[1];
                var zStem = loa * 0.5f - p[2];
                y0 = MathF.Min(y0, y);
                y1 = MathF.Max(y1, y);
                u0 = MathF.Min(u0, up);
                u1 = MathF.Max(u1, up);
                z0 = MathF.Min(z0, zStem);
                z1 = MathF.Max(z1, zStem);
                ring.Add((y, zStem));
            }

            if (y0 > y1 || z0 > z1)
                continue;
            u1 = MathF.Max(u1, u0 + MathF.Max(0.5f, e.Height));

            if (byId.TryGetValue(e.Name!, out var prev))
            {
                byId[e.Name!] = prev with
                {
                    Z0 = MathF.Min(prev.Z0, z0),
                    Z1 = MathF.Max(prev.Z1, z1),
                    Y0 = MathF.Min(prev.Y0, y0),
                    Y1 = MathF.Max(prev.Y1, y1),
                    Up0 = MathF.Min(prev.Up0, u0),
                    Up1 = MathF.Max(prev.Up1, u1),
                    PlanRing = prev.PlanRing.Count >= ring.Count ? prev.PlanRing : ring,
                };
            }
            else
            {
                byId[e.Name!] = new SpaceVolume(e.Name!, e.Deck, z0, z1, y0, y1, u0, u1, ring);
            }
        }

        return byId.Values.OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool TryGetSpace(CadDocument doc, string id, out SpaceVolume space)
    {
        var hit = Spaces(doc).FirstOrDefault(s =>
            string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
        if (hit is null)
        {
            space = null!;
            return false;
        }

        space = hit;
        return true;
    }

    public static IReadOnlyList<SandwichPanel> SandwichPanels(CadDocument doc, float loa)
    {
        var list = new List<SandwichPanel>();
        foreach (var e in doc.Entities ?? [])
        {
            if (e.Name is null || !SandwichName.IsMatch(e.Name))
                continue;
            // Footprints = face-top boxes only (actual plan extents, not solid-stack proxies).
            var n = e.Name;
            if (!n.Contains("-face-top", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!TryBox(e, out var center, out var half))
                continue;

            var deckNum = ParseDeckNum(e.Name);
            var faceT = MathF.Abs(half.Y) * 2f;
            var plateTop = e.Properties is not null
                && e.Properties.TryGetValue("deckUp", out var duEl)
                && duEl.ValueKind == JsonValueKind.Number
                ? (float)duEl.GetDouble()
                : center.Y + MathF.Abs(half.Y);

            var zStem = loa * 0.5f - center.Z;
            var halfZ = MathF.Abs(half.Z);
            var halfY = MathF.Abs(half.X);
            list.Add(new SandwichPanel(
                deckNum,
                plateTop,
                faceT,
                zStem - halfZ,
                zStem + halfZ,
                center.X - halfY,
                center.X + halfY,
                e.Name));
        }

        return list.OrderBy(p => p.DeckNum).ThenBy(p => p.Z0).ThenBy(p => p.Y0).ToList();
    }

    /// <summary>
    /// Cellular sandwich steel mass from face sheets + peri + ribs (box volumes × density).
    /// Does not invent solid-stack thickness for the hollow core.
    /// </summary>
    public static DeckSteelMass SandwichSteelMass(CadDocument doc, int deckNum)
    {
        float volume = 0f;
        float faceArea = 0f;
        var parts = 0;
        foreach (var e in doc.Entities ?? [])
        {
            if (e.Name is null || !SandwichName.IsMatch(e.Name) || ParseDeckNum(e.Name) != deckNum)
                continue;
            if (!IsSandwichSteelPart(e.Name) || !TryBox(e, out _, out var half))
                continue;

            volume += BoxVolumeM3(half);
            parts++;
            if (e.Name.Contains("-face-top", StringComparison.OrdinalIgnoreCase))
                faceArea += 4f * MathF.Abs(half.X) * MathF.Abs(half.Z);
        }

        return new DeckSteelMass(deckNum, volume, volume * SteelDensityKgPerM3, parts, faceArea);
    }

    public static IReadOnlyList<FrameStation> PhysicalFrameStations(CadDocument doc)
    {
        var counts = new Dictionary<string, (float Z, int N)>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in doc.Entities ?? [])
        {
            if (e.Name is null)
                continue;
            var m = FrameAssembly.Match(e.Name);
            if (!m.Success)
                continue;
            if (!e.Name.Contains("-ring-", StringComparison.OrdinalIgnoreCase))
                continue;
            var id = $"frame-F{m.Groups[1].Value}";
            if (!float.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var z))
                continue;
            if (counts.TryGetValue(id, out var cur))
                counts[id] = (z, cur.N + 1);
            else
                counts[id] = (z, 1);
        }

        return counts
            .Select(kv => new FrameStation(kv.Key, kv.Value.Z, kv.Value.N))
            .OrderBy(f => f.ZStem)
            .ToList();
    }

    public static IReadOnlyList<FramePartBox> FramePartsAt(CadDocument doc, string frameId)
    {
        var list = new List<FramePartBox>();
        var prefix = frameId.EndsWith('-') ? frameId : frameId + "-";
        foreach (var e in doc.Entities ?? [])
        {
            if (e.Name is null
                || !e.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !TryBox(e, out var center, out var half))
                continue;
            // Skip join/connector bars that are mostly longitudinal (deep Z, thin Y/Up).
            if (e.Name.Contains("-join-", StringComparison.OrdinalIgnoreCase)
                && MathF.Abs(half.Z) > MathF.Max(MathF.Abs(half.X), MathF.Abs(half.Y)) * 2f)
                continue;

            list.Add(new FramePartBox(
                e.Name,
                center.X - MathF.Abs(half.X),
                center.X + MathF.Abs(half.X),
                center.Y - MathF.Abs(half.Y),
                center.Y + MathF.Abs(half.Y)));
        }

        return list;
    }

    public static bool TryBhPlateMesh(
        CadDocument doc,
        string bhId,
        out IReadOnlyList<float[]> vertices,
        out IReadOnlyList<int> indices)
    {
        var needle = $"bh-{bhId}-plate";
        var ent = doc.Entities.FirstOrDefault(e =>
            e.Name is not null
            && e.Name.Equals(needle, StringComparison.OrdinalIgnoreCase)
            && e.MeshVertices is { Count: >= 3 }
            && e.MeshIndices is { Count: >= 3 });
        if (ent?.MeshVertices is null || ent.MeshIndices is null)
        {
            vertices = Array.Empty<float[]>();
            indices = Array.Empty<int>();
            return false;
        }

        vertices = ent.MeshVertices;
        indices = ent.MeshIndices;
        return true;
    }

    private static bool IsSandwichSteelPart(string name) =>
        name.Contains("-face-top", StringComparison.OrdinalIgnoreCase)
        || name.Contains("-face-bot", StringComparison.OrdinalIgnoreCase)
        || name.Contains("-peri-", StringComparison.OrdinalIgnoreCase)
        || name.Contains("-rib-", StringComparison.OrdinalIgnoreCase);

    private static float BoxVolumeM3(System.Numerics.Vector3 half) =>
        8f * MathF.Abs(half.X) * MathF.Abs(half.Y) * MathF.Abs(half.Z);

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
        return match.Average(p => p.FaceThicknessM);
    }

    public static IReadOnlyList<BhStation> BulkheadStations(CadDocument doc, float loa)
    {
        var byId = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in doc.Entities ?? [])
        {
            if (e.Name is null || !TryBox(e, out var center, out _))
                continue;

            // Prefer semantic stamps (assemblyId / cadPartKind) over name prefixes.
            string? id = null;
            if (e.Properties is not null
                && e.Properties.TryGetValue("cadPartKind", out var kindEl)
                && kindEl.ValueKind == System.Text.Json.JsonValueKind.String
                && string.Equals(kindEl.GetString(), "BulkheadPlate", StringComparison.OrdinalIgnoreCase)
                && e.Properties.TryGetValue("assemblyId", out var aidEl)
                && aidEl.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var aid = aidEl.GetString() ?? "";
                id = aid.StartsWith("BH-", StringComparison.OrdinalIgnoreCase) ? aid[3..] : aid;
            }
            else if (e.Name.StartsWith("bh-", StringComparison.OrdinalIgnoreCase)
                     && e.Name.Contains("-plate", StringComparison.OrdinalIgnoreCase)
                     && !e.Name.Contains("-deckseat", StringComparison.OrdinalIgnoreCase))
            {
                var rest = e.Name["bh-".Length..];
                var cut = rest.IndexOf("-plate", StringComparison.OrdinalIgnoreCase);
                if (cut > 0)
                    id = rest[..cut];
            }

            if (id is null)
                continue;
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
