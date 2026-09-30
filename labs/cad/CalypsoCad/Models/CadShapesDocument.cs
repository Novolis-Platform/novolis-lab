using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Models;

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
