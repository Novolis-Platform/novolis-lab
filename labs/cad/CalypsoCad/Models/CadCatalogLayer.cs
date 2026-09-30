using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Models;

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
