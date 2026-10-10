using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CapitalistSimulator.Sim;

internal sealed record SeaportOfferDef(
    string ProductId,
    double Quality,
    decimal MonthlySupply,
    decimal UnitCost);
