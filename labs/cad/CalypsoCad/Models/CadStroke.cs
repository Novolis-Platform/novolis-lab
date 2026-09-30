using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Models;

internal sealed class CadStroke
{
    public float[]? Color { get; set; }
    public float LineWeightMm { get; set; }
    public string Linetype { get; set; } = "Continuous";
}
