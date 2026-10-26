using System.Text.Json;

namespace CalypsoCad.Generation.Fabrication;

internal static class FabricationJson
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
}
