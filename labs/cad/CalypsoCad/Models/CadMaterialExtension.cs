using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Models;

internal sealed class CadMaterialExtension
{
    public string? Preset { get; set; }
    public float[]? Albedo { get; set; }
    public float Roughness { get; set; } = 0.5f;
    public float Metalness { get; set; }
}
