using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CapitalistSimulator.Sim;

internal sealed record FirmTypeDef(
    string Id,
    FirmKind Kind,
    string Name,
    decimal SetupCost,
    decimal MonthlyCost,
    int Width,
    int Height,
    int LayoutW,
    int LayoutH,
    RetailFamily? RetailFamily,
    ExtractKind? ExtractKind,
    int Size,
    IReadOnlyList<ProductClass> AllowedClasses);
