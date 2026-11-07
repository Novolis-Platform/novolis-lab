#:property OutputType=Exe
#:property TargetFramework=net10.0
#:property ImplicitUsings=enable
#:property Nullable=enable

// Downloads Norkart/Kommunekart Cesium 3D Tiles (b3dm) for the Kristiansand map frame
// and writes a wireframe cache for ArtillerySimulator.
// Source viewer: https://3d.kommunekart.com/ (tiles: waapi.webatlas.no, app key from Kommunekart SPA).

using System.Globalization;
using System.Net.Http.Json;
using System.Numerics;
using System.Text;
using System.Text.Json;

const string ApiKey = "adab053e-2e67-44de-8604-f900e5dc10de"; // embedded in Kommunekart 3D SPA
const string TileBase = "https://waapi.webatlas.no/3d-tiles/tileserver.fcgi";

// Match KristiansandHeightSampler / georef.json
const double SouthLat = 58.06181603859646;
const double NorthLat = 58.20818396140355;
const double WestLon = 7.825738542655645;
const double EastLon = 8.234261457344356;
const float EastMeters = 24008.08f;
const float NorthMeters = 16266.16f;

// Harbor / Kvadraturen / Odderøya focus (still inside map frame).
const double FocusWest = 7.97;
const double FocusEast = 8.05;
const double FocusSouth = 58.125;
const double FocusNorth = 58.165;
const double MaxGeometricError = 12.0;

var mapDir = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map";
var rawDir = Path.Combine(mapDir, "layers", "kommunekart-buildings", "raw");
var outPath = Path.Combine(mapDir, "layers", "kommunekart-buildings.wires.bin");
Directory.CreateDirectory(rawDir);

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };

var westR = DegToRad(FocusWest);
var southR = DegToRad(FocusSouth);
var eastR = DegToRad(FocusEast);
var northR = DegToRad(FocusNorth);

var reuseRaw = args.Any(a => a is "--reuse-raw" or "--from-raw");
var edges = new List<(Vector3 A, Vector3 B)>(256_000);

if (reuseRaw)
{
    var files = Directory.GetFiles(rawDir, "*.b3dm");
    Console.WriteLine($"Reconverting {files.Length} raw b3dm under {rawDir}…");
    var tileIndex = 0;
    foreach (var local in files.OrderBy(f => f, StringComparer.Ordinal))
    {
        tileIndex++;
        try
        {
            var added = ExtractEdgesFromB3dm(local, edges);
            if (tileIndex % 50 == 0 || tileIndex == files.Length)
                Console.WriteLine($"  [{tileIndex}/{files.Length}] +edges → {edges.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL {Path.GetFileName(local)}: {ex.Message}");
        }
    }
}
else
{
    Console.WriteLine("Fetching root tileset…");
    var root = await GetTilesetAsync(http, "tileset.json");
    var queue = new Queue<string>();
    EnqueueOverlapping(root, queue, westR, southR, eastR, northR, preferNested: true);

    var seenTilesets = new HashSet<string>(StringComparer.Ordinal);
    var b3dmUris = new HashSet<string>(StringComparer.Ordinal);
    while (queue.Count > 0 && seenTilesets.Count < 80)
    {
        var rel = queue.Dequeue();
        if (!seenTilesets.Add(rel))
            continue;

        JsonElement ts;
        try
        {
            ts = await GetTilesetAsync(http, rel);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  skip tileset {rel}: {ex.Message}");
            continue;
        }

        Console.WriteLine($"tileset {rel} (queue={queue.Count}, b3dm={b3dmUris.Count})");
        Collect(ts, queue, b3dmUris, westR, southR, eastR, northR);
    }

    Console.WriteLine($"Downloading {b3dmUris.Count} b3dm tiles…");
    var tileIndex = 0;
    foreach (var uri in b3dmUris.OrderBy(u => u, StringComparer.Ordinal))
    {
        tileIndex++;
        var fileName = uri.Split('?', 2)[0];
        var local = Path.Combine(rawDir, fileName);
        try
        {
            if (!File.Exists(local) || new FileInfo(local).Length < 64)
                await DownloadAsync(http, uri, local);

            var added = ExtractEdgesFromB3dm(local, edges);
            Console.WriteLine(
                $"  [{tileIndex}/{b3dmUris.Count}] {fileName} +{added} edges (total {edges.Count})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL {fileName}: {ex.Message}");
        }
    }
}

// Dedup near-identical edges (quantize 0.25 m).
var dedup = new HashSet<ulong>();
var unique = new List<(Vector3 A, Vector3 B)>(edges.Count);
foreach (var (a, b) in edges)
{
    if (!InsideMap(a) || !InsideMap(b))
        continue;
    if (Vector3.DistanceSquared(a, b) < 0.05f)
        continue;
    var key = EdgeKey(a, b);
    if (!dedup.Add(key))
        continue;
    unique.Add((a, b));
}

Console.WriteLine($"Writing {unique.Count} edges → {outPath}");
WriteWires(outPath, unique);
Console.WriteLine("OK");

static bool InsideMap(Vector3 p) =>
    p.X >= 0f && p.Z >= 0f && p.X <= EastMeters && p.Z <= NorthMeters && p.Y > -50f && p.Y < 400f;

static ulong EdgeKey(Vector3 a, Vector3 b)
{
    static int Q(float v) => (int)MathF.Round(v * 4f);
    var ax = Q(a.X); var ay = Q(a.Y); var az = Q(a.Z);
    var bx = Q(b.X); var by = Q(b.Y); var bz = Q(b.Z);
    if (ax > bx || (ax == bx && ay > by) || (ax == bx && ay == by && az > bz))
        (ax, ay, az, bx, by, bz) = (bx, by, bz, ax, ay, az);
    unchecked
    {
        ulong h = (uint)ax;
        h = (h * 16777619u) ^ (uint)ay;
        h = (h * 16777619u) ^ (uint)az;
        h = (h * 16777619u) ^ (uint)bx;
        h = (h * 16777619u) ^ (uint)by;
        h = (h * 16777619u) ^ (uint)bz;
        return h;
    }
}

static void WriteWires(string path, List<(Vector3 A, Vector3 B)> edges)
{
    using var fs = File.Create(path);
    using var w = new BinaryWriter(fs);
    w.Write(0x57424B4Bu); // KKBW
    w.Write((ushort)1);
    w.Write(edges.Count);
    foreach (var (a, b) in edges)
    {
        w.Write(a.X); w.Write(a.Y); w.Write(a.Z);
        w.Write(b.X); w.Write(b.Y); w.Write(b.Z);
    }
}

static async Task DownloadAsync(HttpClient http, string rel, string dest)
{
    var url = AbsUrl(rel);
    var bytes = await http.GetByteArrayAsync(url);
    await File.WriteAllBytesAsync(dest, bytes);
}

static string AbsUrl(string rel)
{
    if (rel.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        return rel.Contains("api_key=", StringComparison.Ordinal) ? rel : rel + (rel.Contains('?', StringComparison.Ordinal) ? "&" : "?") + "api_key=" + ApiKey;
    return $"{TileBase}/{rel}{(rel.Contains('?', StringComparison.Ordinal) ? "&" : "?")}api_key={ApiKey}";
}

static async Task<JsonElement> GetTilesetAsync(HttpClient http, string rel)
{
    var url = AbsUrl(rel);
    await using var stream = await http.GetStreamAsync(url);
    using var doc = await JsonDocument.ParseAsync(stream);
    return doc.RootElement.Clone();
}

static void EnqueueOverlapping(JsonElement tileset, Queue<string> queue, double w, double s, double e, double n, bool preferNested)
{
    if (!tileset.TryGetProperty("root", out var root))
        return;
    WalkEnqueue(root, queue, w, s, e, n, preferNested);
}

static void Collect(JsonElement tileset, Queue<string> queue, HashSet<string> b3dm, double w, double s, double e, double n)
{
    if (!tileset.TryGetProperty("root", out var root))
        return;
    WalkCollect(root, queue, b3dm, w, s, e, n);
}

static void WalkEnqueue(JsonElement node, Queue<string> queue, double w, double s, double e, double n, bool preferNested)
{
    if (!RegionOverlaps(node, w, s, e, n))
        return;

    var uri = ContentUri(node);
    if (uri is not null && uri.Contains("tileset.json", StringComparison.OrdinalIgnoreCase))
        queue.Enqueue(uri);

    if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
    {
        foreach (var child in children.EnumerateArray())
            WalkEnqueue(child, queue, w, s, e, n, preferNested);
    }
}

static void WalkCollect(JsonElement node, Queue<string> queue, HashSet<string> b3dm, double w, double s, double e, double n)
{
    if (!RegionOverlaps(node, w, s, e, n))
        return;

    var ge = node.TryGetProperty("geometricError", out var geEl) && geEl.TryGetDouble(out var g) ? g : 999.0;
    var uri = ContentUri(node);
    if (uri is not null)
    {
        if (uri.Contains("tileset.json", StringComparison.OrdinalIgnoreCase))
            queue.Enqueue(uri);
        else if (uri.Contains(".b3dm", StringComparison.OrdinalIgnoreCase) && ge <= MaxGeometricError)
            b3dm.Add(uri);
    }

    if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
    {
        foreach (var child in children.EnumerateArray())
            WalkCollect(child, queue, b3dm, w, s, e, n);
    }
}

static string? ContentUri(JsonElement node)
{
    if (node.TryGetProperty("content", out var c) && c.TryGetProperty("uri", out var u))
        return u.GetString();
    if (node.TryGetProperty("contents", out var cs) && cs.ValueKind == JsonValueKind.Array && cs.GetArrayLength() > 0)
    {
        var first = cs[0];
        if (first.TryGetProperty("uri", out var u2))
            return u2.GetString();
    }

    return null;
}

static bool RegionOverlaps(JsonElement node, double w, double s, double e, double n)
{
    if (!node.TryGetProperty("boundingVolume", out var bv) || !bv.TryGetProperty("region", out var region))
        return true; // unknown → keep
    if (region.ValueKind != JsonValueKind.Array || region.GetArrayLength() < 4)
        return true;
    var rw = region[0].GetDouble();
    var rs = region[1].GetDouble();
    var re = region[2].GetDouble();
    var rn = region[3].GetDouble();
    return !(re < w || rw > e || rn < s || rs > n);
}

static int ExtractEdgesFromB3dm(string path, List<(Vector3 A, Vector3 B)> edges)
{
    var bytes = File.ReadAllBytes(path);
    if (bytes.Length < 28 || Encoding.ASCII.GetString(bytes, 0, 4) != "b3dm")
        throw new InvalidDataException("not b3dm");

    var ftJsonLen = BitConverter.ToUInt32(bytes, 12);
    var ftBinLen = BitConverter.ToUInt32(bytes, 16);
    var btJsonLen = BitConverter.ToUInt32(bytes, 20);
    var btBinLen = BitConverter.ToUInt32(bytes, 24);
    var featureJson = Encoding.UTF8.GetString(bytes, 28, (int)ftJsonLen).TrimEnd('\0');
    using var ftDoc = JsonDocument.Parse(featureJson);
    var rtc = Vector3.Zero;
    if (ftDoc.RootElement.TryGetProperty("RTC_CENTER", out var rtcEl) && rtcEl.GetArrayLength() >= 3)
    {
        rtc = new Vector3(
            (float)rtcEl[0].GetDouble(),
            (float)rtcEl[1].GetDouble(),
            (float)rtcEl[2].GetDouble());
    }

    var glbStart = 28 + (int)ftJsonLen + (int)ftBinLen + (int)btJsonLen + (int)btBinLen;
    while (glbStart < bytes.Length - 4 && Encoding.ASCII.GetString(bytes, glbStart, 4) != "glTF")
        glbStart++;
    if (glbStart >= bytes.Length - 12)
        throw new InvalidDataException("glTF not found");

    return ExtractEdgesFromGlb(bytes.AsSpan(glbStart), rtc, edges);
}

static int ExtractEdgesFromGlb(ReadOnlySpan<byte> glb, Vector3 rtcEcef, List<(Vector3 A, Vector3 B)> edges)
{
    if (Encoding.ASCII.GetString(glb[..4]) != "glTF")
        throw new InvalidDataException("not glb");
    var jsonLen = BitConverter.ToInt32(glb[12..]);
    var json = Encoding.UTF8.GetString(glb.Slice(20, jsonLen));
    var binOffset = 20 + jsonLen;
    if (binOffset + 8 > glb.Length)
        throw new InvalidDataException("glb truncated");
    var binLen = BitConverter.ToInt32(glb[binOffset..]);
    var bin = glb.Slice(binOffset + 8, binLen);

    using var doc = JsonDocument.Parse(json);
    var root = doc.RootElement;
    if (!root.TryGetProperty("meshes", out var meshes) || !root.TryGetProperty("nodes", out var nodes))
        return 0;

    // World matrix per node (column-major glTF), then ECEF = RTC + R * p.
    var world = new Matrix4x4[nodes.GetArrayLength()];
    for (var i = 0; i < world.Length; i++)
        world[i] = Matrix4x4.Identity;
    ComputeNodeWorldMatrices(nodes, world);

    var before = edges.Count;
    for (var nodeIndex = 0; nodeIndex < nodes.GetArrayLength(); nodeIndex++)
    {
        var node = nodes[nodeIndex];
        if (!node.TryGetProperty("mesh", out var meshEl))
            continue;
        var mesh = meshes[meshEl.GetInt32()];
        if (!mesh.TryGetProperty("primitives", out var prims))
            continue;

        var nodeWorld = world[nodeIndex];
        foreach (var prim in prims.EnumerateArray())
        {
            if (!prim.TryGetProperty("attributes", out var attrs) || !attrs.TryGetProperty("POSITION", out var posAccEl))
                continue;
            var local = ReadVec3Accessor(root, bin, posAccEl.GetInt32());
            if (local.Length == 0)
                continue;

            // Node matrix puts glTF into ECEF-axis deltas; +RTC → ECEF. Height = ENU-up at RTC
            // (ellipsoidal height from ECEF alone is unreliable for these deltas).
            var up = Vector3.Normalize(rtcEcef);
            var verts = new Vector3[local.Length];
            for (var i = 0; i < local.Length; i++)
            {
                var delta = Vector3.Transform(local[i], nodeWorld);
                var ecef = rtcEcef + delta;
                verts[i] = ToWorld(ecef, heightMeters: Vector3.Dot(delta, up));
            }

            // Prefer Cesium outline edges (building silhouettes) over triangle soup.
            if (prim.TryGetProperty("extensions", out var ext)
                && ext.TryGetProperty("CESIUM_primitive_outline", out var outline)
                && outline.TryGetProperty("indices", out var outlineAcc))
            {
                var outlineIdx = ReadIndices(root, bin, outlineAcc.GetInt32());
                for (var i = 0; i + 1 < outlineIdx.Length; i += 2)
                {
                    var i0 = outlineIdx[i];
                    var i1 = outlineIdx[i + 1];
                    if ((uint)i0 >= (uint)verts.Length || (uint)i1 >= (uint)verts.Length)
                        continue;
                    edges.Add((verts[i0], verts[i1]));
                }

                continue;
            }

            int[] indices;
            if (prim.TryGetProperty("indices", out var idxEl))
                indices = ReadIndices(root, bin, idxEl.GetInt32());
            else
            {
                indices = new int[local.Length];
                for (var i = 0; i < indices.Length; i++)
                    indices[i] = i;
            }

            var mode = prim.TryGetProperty("mode", out var modeEl) ? modeEl.GetInt32() : 4;
            if (mode != 4)
                continue;

            for (var i = 0; i + 2 < indices.Length; i += 3)
            {
                var i0 = indices[i];
                var i1 = indices[i + 1];
                var i2 = indices[i + 2];
                if ((uint)i0 >= (uint)verts.Length || (uint)i1 >= (uint)verts.Length || (uint)i2 >= (uint)verts.Length)
                    continue;
                edges.Add((verts[i0], verts[i1]));
                edges.Add((verts[i1], verts[i2]));
                edges.Add((verts[i2], verts[i0]));
            }
        }
    }

    return edges.Count - before;
}

static void ComputeNodeWorldMatrices(JsonElement nodes, Matrix4x4[] world)
{
    var parent = new int[world.Length];
    Array.Fill(parent, -1);
    for (var i = 0; i < nodes.GetArrayLength(); i++)
    {
        var node = nodes[i];
        if (!node.TryGetProperty("children", out var ch) || ch.ValueKind != JsonValueKind.Array)
            continue;
        foreach (var c in ch.EnumerateArray())
            parent[c.GetInt32()] = i;
    }

    Matrix4x4 LocalOf(JsonElement node)
    {
        if (node.TryGetProperty("matrix", out var m) && m.ValueKind == JsonValueKind.Array && m.GetArrayLength() >= 16)
        {
            // glTF = column-major for column vectors. System.Numerics uses row vectors (v*M),
            // so store transpose(M_gltf).
            return new Matrix4x4(
                (float)m[0].GetDouble(), (float)m[1].GetDouble(), (float)m[2].GetDouble(), (float)m[3].GetDouble(),
                (float)m[4].GetDouble(), (float)m[5].GetDouble(), (float)m[6].GetDouble(), (float)m[7].GetDouble(),
                (float)m[8].GetDouble(), (float)m[9].GetDouble(), (float)m[10].GetDouble(), (float)m[11].GetDouble(),
                (float)m[12].GetDouble(), (float)m[13].GetDouble(), (float)m[14].GetDouble(), (float)m[15].GetDouble());
        }

        var t = Vector3.Zero;
        var s = Vector3.One;
        if (node.TryGetProperty("translation", out var te) && te.GetArrayLength() >= 3)
            t = new Vector3((float)te[0].GetDouble(), (float)te[1].GetDouble(), (float)te[2].GetDouble());
        if (node.TryGetProperty("scale", out var se) && se.GetArrayLength() >= 3)
            s = new Vector3((float)se[0].GetDouble(), (float)se[1].GetDouble(), (float)se[2].GetDouble());
        var r = Quaternion.Identity;
        if (node.TryGetProperty("rotation", out var re) && re.GetArrayLength() >= 4)
            r = new Quaternion((float)re[0].GetDouble(), (float)re[1].GetDouble(), (float)re[2].GetDouble(), (float)re[3].GetDouble());
        return Matrix4x4.CreateScale(s) * Matrix4x4.CreateFromQuaternion(r) * Matrix4x4.CreateTranslation(t);
    }

    bool[] done = new bool[world.Length];
    Matrix4x4 WorldOf(int i)
    {
        if (done[i])
            return world[i];
        var local = LocalOf(nodes[i]);
        world[i] = parent[i] < 0 ? local : local * WorldOf(parent[i]);
        done[i] = true;
        return world[i];
    }

    for (var i = 0; i < world.Length; i++)
        WorldOf(i);
}

static Vector3[] ReadVec3Accessor(JsonElement root, ReadOnlySpan<byte> bin, int accessorIndex)
{
    var acc = root.GetProperty("accessors")[accessorIndex];
    var count = acc.GetProperty("count").GetInt32();
    var componentType = acc.GetProperty("componentType").GetInt32();
    var bufferView = acc.GetProperty("bufferView").GetInt32();
    var byteOffset = acc.TryGetProperty("byteOffset", out var bo) ? bo.GetInt32() : 0;
    var bv = root.GetProperty("bufferViews")[bufferView];
    var viewOffset = bv.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0;
    var compSize = componentType switch
    {
        5120 or 5121 => 1,
        5122 or 5123 => 2,
        5126 => 4,
        _ => throw new InvalidDataException($"POSITION componentType {componentType}"),
    };
    var stride = bv.TryGetProperty("byteStride", out var bs) ? bs.GetInt32() : compSize * 3;
    if (stride < compSize * 3)
        stride = compSize * 3;
    var start = viewOffset + byteOffset;
    var result = new Vector3[count];
    for (var i = 0; i < count; i++)
    {
        var o = start + i * stride;
        result[i] = componentType switch
        {
            5126 => new Vector3(
                BitConverter.ToSingle(bin[o..]),
                BitConverter.ToSingle(bin[(o + 4)..]),
                BitConverter.ToSingle(bin[(o + 8)..])),
            5123 => new Vector3(
                BitConverter.ToUInt16(bin[o..]),
                BitConverter.ToUInt16(bin[(o + 2)..]),
                BitConverter.ToUInt16(bin[(o + 4)..])),
            5122 => new Vector3(
                BitConverter.ToInt16(bin[o..]),
                BitConverter.ToInt16(bin[(o + 2)..]),
                BitConverter.ToInt16(bin[(o + 4)..])),
            _ => throw new InvalidDataException($"POSITION componentType {componentType}"),
        };
    }

    return result;
}

static int[] ReadIndices(JsonElement root, ReadOnlySpan<byte> bin, int accessorIndex)
{
    var acc = root.GetProperty("accessors")[accessorIndex];
    var count = acc.GetProperty("count").GetInt32();
    var componentType = acc.GetProperty("componentType").GetInt32();
    var bufferView = acc.GetProperty("bufferView").GetInt32();
    var byteOffset = acc.TryGetProperty("byteOffset", out var bo) ? bo.GetInt32() : 0;
    var bv = root.GetProperty("bufferViews")[bufferView];
    var viewOffset = bv.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0;
    var start = viewOffset + byteOffset;
    var result = new int[count];
    for (var i = 0; i < count; i++)
    {
        result[i] = componentType switch
        {
            5121 => bin[start + i],
            5123 => BitConverter.ToUInt16(bin[(start + i * 2)..]),
            5125 => (int)BitConverter.ToUInt32(bin[(start + i * 4)..]),
            _ => throw new InvalidDataException($"index componentType {componentType}"),
        };
    }

    return result;
}

/// <summary>ECEF → Kristiansand world meters. XZ from geodetic; Y from ENU-up at RTC.</summary>
static Vector3 ToWorld(Vector3 ecef, float heightMeters)
{
    EcefToGeodetic(ecef.X, ecef.Y, ecef.Z, out var latDeg, out var lonDeg, out _);
    var x = (float)((lonDeg - WestLon) / (EastLon - WestLon) * EastMeters);
    var z = (float)((latDeg - SouthLat) / (NorthLat - SouthLat) * NorthMeters);
    return new Vector3(x, heightMeters, z);
}

static void EcefToGeodetic(double x, double y, double z, out double latDeg, out double lonDeg, out double h)
{
    const double A = 6378137.0;
    const double E2 = 6.69437999014e-3;
    var lon = Math.Atan2(y, x);
    var p = Math.Sqrt(x * x + y * y);
    var lat = Math.Atan2(z, p * (1.0 - E2));
    for (var i = 0; i < 8; i++)
    {
        var sinLat = Math.Sin(lat);
        var n = A / Math.Sqrt(1.0 - E2 * sinLat * sinLat);
        h = p / Math.Cos(lat) - n;
        lat = Math.Atan2(z, p * (1.0 - E2 * n / (n + h)));
    }

    var sinL = Math.Sin(lat);
    var nFinal = A / Math.Sqrt(1.0 - E2 * sinL * sinL);
    h = p / Math.Cos(lat) - nFinal;
    latDeg = lat * (180.0 / Math.PI);
    lonDeg = lon * (180.0 / Math.PI);
}

static double DegToRad(double deg) => deg * (Math.PI / 180.0);
