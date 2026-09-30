using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Models;

internal sealed class CadShape
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Name { get; set; }
    public CadShapeExtensions? Extensions { get; set; }
}
