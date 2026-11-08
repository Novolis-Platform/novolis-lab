using System.Text.Json;
using System.Text.Json.Serialization;
using CalypsoCad.Model;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Publication;

/// <summary>DTO for round-tripping the semantic registry through <see cref="CadDocument.Properties"/>.</summary>
internal sealed class SemanticRegistrySnapshot
{
    public const string DocumentPropertyKey = "semanticRegistry";

    public List<AssemblyDto> Assemblies { get; set; } = [];
    public List<PartDto> Parts { get; set; } = [];
    public List<ReferenceDto> References { get; set; } = [];
    public List<ConnectionDto> Connections { get; set; } = [];

    public sealed class AssemblyDto
    {
        public string Id { get; set; } = "";
        public string System { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Kind { get; set; } = "";
        public string? ParentId { get; set; }
        public float? StationZStem { get; set; }
        public float? ExtentZ0 { get; set; }
        public float? ExtentZ1 { get; set; }
        public int Functions { get; set; }
        public List<string> MemberPartIds { get; set; } = [];
    }

    public sealed class PartDto
    {
        public string Id { get; set; } = "";
        public string AssemblyId { get; set; } = "";
        public string System { get; set; } = "";
        public string Kind { get; set; } = "";
        public int Functions { get; set; }
        public string DisplayName { get; set; } = "";
        public float? StationZStem { get; set; }
        public float? DeckUp { get; set; }
        public string? FabPartId { get; set; }
        public string? LegacyRole { get; set; }
        public Guid? EntityId { get; set; }
        public bool Classified { get; set; } = true;
    }

    public sealed class ReferenceDto
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? ParentId { get; set; }
        public Guid? EntityId { get; set; }
        public float? StationZStem { get; set; }
        public float? DeckUp { get; set; }
        public string? CompartmentId { get; set; }
    }

    public sealed class ConnectionDto
    {
        public string Id { get; set; } = "";
        public string? PartA { get; set; }
        public string? PartB { get; set; }
        public string? AssemblyA { get; set; }
        public string? AssemblyB { get; set; }
        public string JointType { get; set; } = "";
        public string Interface { get; set; } = "";
        public string Specification { get; set; } = "";
        public bool GeometryValid { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static SemanticRegistrySnapshot Capture(ShipPartRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var snap = new SemanticRegistrySnapshot();
        foreach (var a in registry.Assemblies)
        {
            snap.Assemblies.Add(new AssemblyDto
            {
                Id = a.Id.Value,
                System = a.System.ToString(),
                DisplayName = a.DisplayName,
                Kind = a.Kind.ToString(),
                ParentId = a.ParentAssemblyId?.Value,
                StationZStem = a.StationZStem,
                ExtentZ0 = a.ExtentZ0,
                ExtentZ1 = a.ExtentZ1,
                Functions = (int)a.Functions,
                MemberPartIds = a.MemberPartIds?.Select(p => p.Value).ToList() ?? [],
            });
        }

        foreach (var p in registry.Parts)
        {
            snap.Parts.Add(new PartDto
            {
                Id = p.Id.Value,
                AssemblyId = p.Assembly.Value,
                System = p.System.ToString(),
                Kind = p.Kind.ToString(),
                Functions = (int)p.Functions,
                DisplayName = p.DisplayName,
                StationZStem = p.StationZStem,
                DeckUp = p.DeckUp,
                FabPartId = p.FabPartId,
                LegacyRole = p.LegacyRole,
                EntityId = p.EntityId,
                Classified = p.Classified,
            });
        }

        foreach (var r in registry.References)
        {
            snap.References.Add(new ReferenceDto
            {
                Id = r.Id,
                Kind = r.Kind.ToString(),
                DisplayName = r.DisplayName,
                ParentId = r.ParentAssemblyId?.Value,
                EntityId = r.EntityId,
                StationZStem = r.StationZStem,
                DeckUp = r.DeckUp,
                CompartmentId = r.CompartmentId,
            });
        }

        foreach (var c in registry.Connections)
        {
            snap.Connections.Add(new ConnectionDto
            {
                Id = c.Id,
                PartA = c.PartA?.Value,
                PartB = c.PartB?.Value,
                AssemblyA = c.AssemblyA?.Value,
                AssemblyB = c.AssemblyB?.Value,
                JointType = c.JointType,
                Interface = c.Interface,
                Specification = c.Specification,
                GeometryValid = c.GeometryValid,
            });
        }

        return snap;
    }

    public void ApplyTo(ShipPartRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        foreach (var a in Assemblies)
        {
            if (!Enum.TryParse<CadSystem>(a.System, ignoreCase: true, out var system))
                system = CadSystem.Organization;
            if (!Enum.TryParse<CadAssemblyKind>(a.Kind, ignoreCase: true, out var kind))
                kind = CadAssemblyKind.Other;
            registry.RegisterAssembly(new CadAssembly(
                AssemblyId.Of(a.Id),
                system,
                a.DisplayName,
                a.StationZStem,
                (PartFunction)a.Functions,
                a.MemberPartIds.Select(PartId.Of).ToList(),
                kind,
                string.IsNullOrWhiteSpace(a.ParentId) ? null : AssemblyId.Of(a.ParentId),
                a.ExtentZ0,
                a.ExtentZ1));
        }

        foreach (var p in Parts)
        {
            if (!Enum.TryParse<CadSystem>(p.System, ignoreCase: true, out var system))
                system = CadSystem.Transverse;
            if (!Enum.TryParse<CadPartKind>(p.Kind, ignoreCase: true, out var kind))
                kind = CadPartKind.Unknown;
            registry.RegisterPart(new CadPart(
                PartId.Of(p.Id),
                AssemblyId.Of(p.AssemblyId),
                system,
                kind,
                (PartFunction)p.Functions,
                p.DisplayName,
                StationZStem: p.StationZStem,
                DeckUp: p.DeckUp,
                FabPartId: p.FabPartId,
                LegacyRole: p.LegacyRole,
                EntityId: p.EntityId,
                Classified: p.Classified));
        }

        foreach (var r in References)
        {
            if (!Enum.TryParse<CadReferenceKind>(r.Kind, ignoreCase: true, out var kind))
                kind = CadReferenceKind.Other;
            registry.RegisterReference(new CadReferenceObject(
                r.Id,
                kind,
                r.DisplayName,
                string.IsNullOrWhiteSpace(r.ParentId) ? null : AssemblyId.Of(r.ParentId),
                r.EntityId,
                r.StationZStem,
                r.DeckUp,
                r.CompartmentId));
        }

        foreach (var c in Connections)
        {
            registry.RegisterConnection(new StructuralConnection(
                c.Id,
                string.IsNullOrWhiteSpace(c.PartA) ? null : PartId.Of(c.PartA),
                string.IsNullOrWhiteSpace(c.PartB) ? null : PartId.Of(c.PartB),
                string.IsNullOrWhiteSpace(c.AssemblyA) ? null : AssemblyId.Of(c.AssemblyA),
                string.IsNullOrWhiteSpace(c.AssemblyB) ? null : AssemblyId.Of(c.AssemblyB),
                c.JointType,
                c.Interface,
                c.Specification,
                c.GeometryValid));
        }
    }

    public static void WriteToDocument(CadDocument doc, ShipPartRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(registry);
        ShipHierarchyBuilder.EnsureCanonicalHierarchy(registry, doc);
        doc.Properties ??= new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        doc.Properties[DocumentPropertyKey] = JsonSerializer.SerializeToElement(Capture(registry), JsonOptions);
    }

    public static bool TryReadFromDocument(CadDocument doc, ShipPartRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(registry);
        if (doc.Properties is null
            || !doc.Properties.TryGetValue(DocumentPropertyKey, out var el))
            return false;

        var snap = el.Deserialize<SemanticRegistrySnapshot>(JsonOptions);
        if (snap is null)
            return false;
        snap.ApplyTo(registry);
        return true;
    }
}
