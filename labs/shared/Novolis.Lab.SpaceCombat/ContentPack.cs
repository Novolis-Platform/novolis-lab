using System.IO.Compression;
using System.Text.Json;
using Novolis.Simulation.SpaceCombat;

namespace Novolis.Lab.SpaceCombat;

public sealed class ContentPack : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ZipArchive? _zip;
    private readonly RuntimeManifest _runtime;
    private readonly Dictionary<string, MeshData> _meshes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _sfx = new(StringComparer.Ordinal);

    private ContentPack(ZipArchive? zip, RuntimeManifest runtime)
    {
        _zip = zip;
        _runtime = runtime;
    }

    public bool HasPack => _zip is not null;
    public IReadOnlyList<RuntimeMission> Missions => _runtime.Missions;
    public IReadOnlyDictionary<string, RuntimeCraft> Craft => _runtime.Craft;

    public static ContentPack TryLoad(string contentDir)
    {
        var packPath = Path.Combine(contentDir, "freightwing.novpack");
        if (!File.Exists(packPath))
            return new ContentPack(null, CreateFallbackRuntime());

        var zip = ZipFile.OpenRead(packPath);
        using var runtimeStream = zip.GetEntry("runtime.json")?.Open()
                                  ?? throw new InvalidDataException("novpack missing runtime.json");
        var runtime = JsonSerializer.Deserialize<RuntimeManifest>(runtimeStream, JsonOpts)
                      ?? throw new InvalidDataException("runtime.json invalid");
        return new ContentPack(zip, runtime);
    }

    public CraftProfile? TryGetProfile(string craftId)
    {
        if (!_runtime.Craft.TryGetValue(craftId, out var c))
            return null;
        return new CraftProfile
        {
            Id = c.Id,
            Role = c.Role.ToLowerInvariant() switch
            {
                "freighter" => CraftRole.Freighter,
                "hostile" => CraftRole.Hostile,
                _ => CraftRole.Fighter,
            },
            MaxSpeed = c.MaxSpeed,
            Acceleration = c.Acceleration,
            TurnRate = c.TurnRate,
            HitRadius = c.HitRadius,
            MaxShield = c.Shield,
            MaxHull = c.Hull,
            MeshId = c.MeshId,
            MinSpeed = c.Role.Equals("freighter", StringComparison.OrdinalIgnoreCase) ? 4f : 6f,
            Deceleration = c.Acceleration * 0.8f,
            Drag = c.Role.Equals("freighter", StringComparison.OrdinalIgnoreCase) ? 0.45f : 0.35f,
        };
    }

    public CraftProfile ProfileByRole(CraftRole role)
    {
        var key = role switch
        {
            CraftRole.Freighter => "freighter",
            CraftRole.Hostile => "hostile",
            _ => "fighter",
        };
        if (_runtime.Roles.TryGetValue(key, out var id))
        {
            var p = TryGetProfile(id);
            if (p is not null)
                return p;
        }

        return role switch
        {
            CraftRole.Freighter => CraftProfile.FreighterDefault,
            CraftRole.Hostile => CraftProfile.HostileDefault,
            _ => CraftProfile.FighterDefault,
        };
    }

    public MeshData? TryGetMesh(string? meshId)
    {
        if (meshId is null || _zip is null)
            return null;
        if (_meshes.TryGetValue(meshId, out var cached))
            return cached;
        if (!_runtime.Meshes.TryGetValue(meshId, out var path))
            return null;
        var entry = _zip.GetEntry(path);
        if (entry is null)
            return null;
        using var stream = entry.Open();
        var dto = JsonSerializer.Deserialize<MeshDto>(stream, JsonOpts);
        if (dto is null)
            return null;
        var mesh = new MeshData(dto.Positions, dto.Indices);
        _meshes[meshId] = mesh;
        return mesh;
    }

    public byte[]? TryGetSfxWav(string role)
    {
        if (_zip is null)
            return null;
        foreach (var (id, sfx) in _runtime.Sfx)
        {
            if (!sfx.Role.Equals(role, StringComparison.OrdinalIgnoreCase))
                continue;
            if (_sfx.TryGetValue(id, out var cached))
                return cached;
            var entry = _zip.GetEntry(sfx.Path);
            if (entry is null)
                continue;
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();
            _sfx[id] = bytes;
            return bytes;
        }

        return null;
    }

    public void Dispose() => _zip?.Dispose();

    private static RuntimeManifest CreateFallbackRuntime() => new()
    {
        Roles =
        {
            ["freighter"] = "fallback_freighter",
            ["fighter"] = "fallback_fighter",
            ["hostile"] = "fallback_hostile",
        },
        Craft =
        {
            ["fallback_freighter"] = new RuntimeCraft
            {
                Id = "fallback_freighter", Role = "freighter", MaxSpeed = 18, Acceleration = 10,
                TurnRate = 0.9f, HitRadius = 6.5f, Shield = 1.4f, Hull = 1.2f,
            },
            ["fallback_fighter"] = new RuntimeCraft
            {
                Id = "fallback_fighter", Role = "fighter", MaxSpeed = 48, Acceleration = 28,
                TurnRate = 2.2f, HitRadius = 2.6f, Shield = 1f, Hull = 1f,
            },
            ["fallback_hostile"] = new RuntimeCraft
            {
                Id = "fallback_hostile", Role = "hostile", MaxSpeed = 42, Acceleration = 24,
                TurnRate = 2.4f, HitRadius = 2.4f, Shield = 0.35f, Hull = 0.7f,
            },
        },
        Missions =
        [
            new RuntimeMission
            {
                Id = "fallback_m1", UnlockIndex = 0, FreighterCraftId = "fallback_freighter",
                FighterCraftId = "fallback_fighter", HostileCraftId = "fallback_hostile",
                HostileCount = 5, ProtectSeconds = 40, DestroyRequired = 4,
            },
            new RuntimeMission
            {
                Id = "fallback_m2", UnlockIndex = 1, FreighterCraftId = "fallback_freighter",
                FighterCraftId = "fallback_fighter", HostileCraftId = "fallback_hostile",
                HostileCount = 7, ProtectSeconds = 50, DestroyRequired = 5,
            },
            new RuntimeMission
            {
                Id = "fallback_m3", UnlockIndex = 2, FreighterCraftId = "fallback_freighter",
                FighterCraftId = "fallback_fighter", HostileCraftId = "fallback_hostile",
                HostileCount = 8, ProtectSeconds = 35, DestroyRequired = 6,
            },
        ],
    };
}

public sealed class MeshData(float[] positions, int[] indices)
{
    public float[] Positions { get; } = positions;
    public int[] Indices { get; } = indices;
}

public sealed class RuntimeManifest
{
    public Dictionary<string, string> Meshes { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, RuntimeCraft> Craft { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Roles { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, RuntimeSfx> Sfx { get; set; } = new(StringComparer.Ordinal);
    public List<RuntimeMission> Missions { get; set; } = [];
}

public sealed class RuntimeCraft
{
    public string Id { get; set; } = "";
    public string Role { get; set; } = "";
    public string? MeshId { get; set; }
    public float MaxSpeed { get; set; }
    public float Acceleration { get; set; }
    public float TurnRate { get; set; }
    public float HitRadius { get; set; }
    public float Shield { get; set; }
    public float Hull { get; set; }
}

public sealed class RuntimeSfx
{
    public string Path { get; set; } = "";
    public string Role { get; set; } = "";
}

public sealed class RuntimeMission
{
    public string Id { get; set; } = "";
    public int UnlockIndex { get; set; }
    public string FreighterCraftId { get; set; } = "";
    public string FighterCraftId { get; set; } = "";
    public string HostileCraftId { get; set; } = "";
    public int HostileCount { get; set; }
    public float ProtectSeconds { get; set; }
    public int DestroyRequired { get; set; }
}

internal sealed class MeshDto
{
    public string SourceKey { get; set; } = "";
    public float[] Positions { get; set; } = [];
    public int[] Indices { get; set; } = [];
}
