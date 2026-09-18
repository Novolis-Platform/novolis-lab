using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Models;

/// <summary>JSON options for Calypso .cadjson / sidecar interchange.</summary>
internal static class CadJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };
}

/// <summary>Calypso layers catalog sidecar (<c>novolis.cad.layers</c>).</summary>
internal sealed class CadLayersDocument
{
    public string Format { get; set; } = "novolis.cad.layers";
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "layers";
    public string Standard { get; set; } = "custom";
    public string? StandardVersion { get; set; }
    public CadGenerator Generator { get; set; } = new() { Name = "CalypsoCad" };
    public string? CreatedAt { get; set; }
    public string? ModifiedAt { get; set; }
    public List<CadCatalogLayer> Layers { get; set; } = [];
}

internal sealed class CadCatalogLayer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Discipline { get; set; }
    public string? Major { get; set; }
    public List<string>? Minor { get; set; }
    public string? Description { get; set; }
    public float[]? DefaultColor { get; set; }
    public float DefaultLineWeightMm { get; set; }
    public string DefaultLinetype { get; set; } = "Continuous";
    public bool Plot { get; set; } = true;
}

/// <summary>Calypso shapes catalog sidecar (<c>novolis.cad.shape</c>) with appearance/material extensions.</summary>
internal sealed class CadShapesDocument
{
    public string Format { get; set; } = "novolis.cad.shape";
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "shapes";
    public CadGenerator Generator { get; set; } = new() { Name = "CalypsoCad" };
    public string? CreatedAt { get; set; }
    public string? ModifiedAt { get; set; }
    public string? BaseDocument { get; set; }
    public List<CadShape> Shapes { get; set; } = [];
}

internal sealed class CadShape
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Name { get; set; }
    public CadShapeExtensions? Extensions { get; set; }
}

internal sealed class CadShapeExtensions
{
    public CadAppearanceExtension? Appearance { get; set; }
    public CadMaterialExtension? Material { get; set; }
}

internal sealed class CadAppearanceExtension
{
    public CadFill? Fill { get; set; }
    public CadStroke? Stroke { get; set; }
    public int? ColorIndex { get; set; }
}

internal sealed class CadFill
{
    public bool Enabled { get; set; } = true;
    public float[]? Color { get; set; }
}

internal sealed class CadStroke
{
    public float[]? Color { get; set; }
    public float LineWeightMm { get; set; }
    public string Linetype { get; set; } = "Continuous";
}

internal sealed class CadMaterialExtension
{
    public string? Preset { get; set; }
    public float[]? Albedo { get; set; }
    public float Roughness { get; set; } = 0.5f;
    public float Metalness { get; set; }
}
