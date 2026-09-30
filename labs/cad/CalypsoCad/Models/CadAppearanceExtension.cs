using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Models;

internal sealed class CadAppearanceExtension
{
    public CadFill? Fill { get; set; }
    public CadStroke? Stroke { get; set; }
    public int? ColorIndex { get; set; }
}
