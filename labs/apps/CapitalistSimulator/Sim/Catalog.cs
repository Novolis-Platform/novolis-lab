using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CapitalistSimulator.Sim;

internal sealed record ProductDef(
    string Id,
    string Name,
    ProductClass Class,
    decimal BasePrice,
    double Necessity);
