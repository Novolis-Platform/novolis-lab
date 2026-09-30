using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Models;

internal sealed class CadFill
{
    public bool Enabled { get; set; } = true;
    public float[]? Color { get; set; }
}
