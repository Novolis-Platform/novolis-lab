using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Models;

internal sealed class CadShapeExtensions
{
    public CadAppearanceExtension? Appearance { get; set; }
    public CadMaterialExtension? Material { get; set; }
}
