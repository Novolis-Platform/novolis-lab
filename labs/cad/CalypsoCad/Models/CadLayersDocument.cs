using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Models;

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
