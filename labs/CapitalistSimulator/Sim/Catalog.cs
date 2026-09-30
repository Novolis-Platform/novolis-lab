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

internal sealed record RecipeInput(string Id, decimal Qty);

internal sealed record RecipeDef(string Output, IReadOnlyList<RecipeInput> Inputs, int Hours);

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

internal sealed record SeaportOfferDef(
    string ProductId,
    double Quality,
    decimal MonthlySupply,
    decimal UnitCost);

internal sealed class GameCatalog
{
    public IReadOnlyDictionary<string, ProductDef> Products { get; }
    public IReadOnlyList<RecipeDef> Recipes { get; }
    public IReadOnlyDictionary<string, FirmTypeDef> FirmTypes { get; }
    public IReadOnlyList<SeaportOfferDef> Seaport { get; }
    public IReadOnlyDictionary<string, RecipeDef> RecipesByOutput { get; }

    public GameCatalog(
        IReadOnlyList<ProductDef> products,
        IReadOnlyList<RecipeDef> recipes,
        IReadOnlyList<FirmTypeDef> firmTypes,
        IReadOnlyList<SeaportOfferDef> seaport)
    {
        Products = products.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        Recipes = recipes;
        RecipesByOutput = recipes.ToDictionary(r => r.Output, StringComparer.OrdinalIgnoreCase);
        FirmTypes = firmTypes.ToDictionary(f => f.Id, StringComparer.OrdinalIgnoreCase);
        Seaport = seaport;
    }

    public static GameCatalog LoadEmbedded()
    {
        var products = ReadJson<List<ProductDto>>("products.json")
            .Select(p => new ProductDef(
                p.Id,
                p.Name,
                Enum.Parse<ProductClass>(p.Class, ignoreCase: true),
                p.BasePrice,
                p.Necessity))
            .ToList();

        var recipes = ReadJson<List<RecipeDto>>("recipes.json")
            .Select(r => new RecipeDef(
                r.Output,
                r.Inputs.Select(i => new RecipeInput(i.Id, i.Qty)).ToList(),
                r.Hours))
            .ToList();

        var firmTypes = ReadJson<List<FirmTypeDto>>("firm_types.json")
            .Select(f => new FirmTypeDef(
                f.Id,
                Enum.Parse<FirmKind>(f.Kind, ignoreCase: true),
                f.Name,
                f.SetupCost,
                f.MonthlyCost,
                f.Width,
                f.Height,
                f.LayoutW,
                f.LayoutH,
                string.IsNullOrEmpty(f.RetailFamily) ? null : Enum.Parse<RetailFamily>(f.RetailFamily, ignoreCase: true),
                string.IsNullOrEmpty(f.ExtractKind) ? null : Enum.Parse<ExtractKind>(f.ExtractKind, ignoreCase: true),
                f.Size,
                (f.AllowedClasses ?? []).Select(c => Enum.Parse<ProductClass>(c, ignoreCase: true)).ToList()))
            .ToList();

        var seaport = ReadJson<List<SeaportDto>>("seaport.json")
            .Select(s => new SeaportOfferDef(s.ProductId, s.Quality, s.MonthlySupply, s.UnitCost))
            .ToList();

        return new GameCatalog(products, recipes, firmTypes, seaport);
    }

    private static T ReadJson<T>(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded resource not found: {fileName}");
        using var stream = asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Cannot open resource: {name}");
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize {fileName}");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed class ProductDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Class { get; set; } = "";
        public decimal BasePrice { get; set; }
        public double Necessity { get; set; }
    }

    private sealed class RecipeDto
    {
        public string Output { get; set; } = "";
        public List<RecipeInputDto> Inputs { get; set; } = [];
        public int Hours { get; set; } = 1;
    }

    private sealed class RecipeInputDto
    {
        public string Id { get; set; } = "";
        public decimal Qty { get; set; }
    }

    private sealed class FirmTypeDto
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal SetupCost { get; set; }
        public decimal MonthlyCost { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int LayoutW { get; set; }
        public int LayoutH { get; set; }
        public string? RetailFamily { get; set; }
        public string? ExtractKind { get; set; }
        public int Size { get; set; } = 1;
        public List<string>? AllowedClasses { get; set; }
    }

    private sealed class SeaportDto
    {
        public string ProductId { get; set; } = "";
        public double Quality { get; set; }
        public decimal MonthlySupply { get; set; }
        public decimal UnitCost { get; set; }
    }
}
