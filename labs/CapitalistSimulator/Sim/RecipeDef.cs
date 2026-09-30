using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CapitalistSimulator.Sim;

internal sealed record RecipeDef(string Output, IReadOnlyList<RecipeInput> Inputs, int Hours);
