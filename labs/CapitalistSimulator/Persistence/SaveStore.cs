using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class SaveStore
{
    private readonly string _root;

    public SaveStore(string? root = null)
    {
        _root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "CapitalistSimulator",
            "saves");
        Directory.CreateDirectory(_root);
    }

    public string Root => _root;

    public void Save(GameWorld world, string name = "autosave")
    {
        var path = Path.Combine(_root, Sanitize(name) + ".json");
        var dto = SaveMapper.ToDto(world);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(path, json);
    }

    public GameWorld? Load(string name = "autosave")
    {
        var path = Path.Combine(_root, Sanitize(name) + ".json");
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<SaveDto>(json, JsonOptions);
        return dto is null ? null : SaveMapper.FromDto(dto);
    }

    public IReadOnlyList<string> List() =>
        Directory.Exists(_root)
            ? Directory.GetFiles(_root, "*.json").Select(Path.GetFileNameWithoutExtension).Where(n => n is not null).Cast<string>().ToList()
            : [];

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "autosave" : name.Trim();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };
}

internal static class SaveMapper
{
    public static SaveDto ToDto(GameWorld world) => new()
    {
        Scenario = world.Scenario,
        Day = world.Day,
        Seed = world.Seed,
        Speed = world.Speed,
        Paused = world.Paused,
        SelectedCityName = world.SelectedCityName,
        ScenarioTargetProfit = world.ScenarioTargetProfit,
        ScenarioMaxDays = world.ScenarioMaxDays,
        Corporations = world.Corporations.Select(c => new CorpDto
        {
            Id = c.Id.Value,
            Name = c.Name,
            IsPlayer = c.IsPlayer,
            IsAi = c.IsAi,
            Cash = c.Cash,
            OpeningCash = c.OpeningCash,
            BrandStrategy = c.BrandStrategy,
            SharesOutstanding = c.SharesOutstanding,
            SharePrice = c.SharePrice,
            DividendPerShare = c.DividendPerShare,
            LastYearProfit = c.LastYearProfit,
            AiAggressiveness = c.AiAggressiveness,
            Retired = c.Retired,
            Brands = c.Brands.ToDictionary(kv => kv.Key, kv => new BrandDto { Awareness = kv.Value.Awareness, Loyalty = kv.Value.Loyalty }),
            Tech = c.Tech.ProductTech.ToDictionary(kv => kv.Key, kv => kv.Value),
            Loans = c.Loans.Select(l => new LoanDto { Principal = l.Principal, MonthlyRate = l.MonthlyRate }).ToList(),
            Hq = new HqDto
            {
                FinanceAutoDividend = c.Hq.FinanceAutoDividend,
                MarketingAutoAds = c.Hq.MarketingAutoAds,
                ImportPreferInternal = c.Hq.ImportPreferInternal,
                RdAutoStart = c.Hq.RdAutoStart,
            },
            MonthlyPnl = c.MonthlyPnl.ToArray(),
            PnlCursor = c.PnlCursor,
        }).ToList(),
        Cities = world.Cities.Select(c => new CityDto
        {
            Id = c.Id.Value,
            Name = c.Name,
            Width = c.Width,
            Height = c.Height,
            SpendingLevel = c.SpendingLevel,
            SalaryLevel = c.SalaryLevel,
            Climate = c.Climate,
            Population = c.Population,
            Tiles = FlattenTiles(c),
        }).ToList(),
        Firms = world.Firms.Select(f => new FirmDto
        {
            Id = f.Id.Value,
            Owner = f.Owner.Value,
            CityId = f.CityId.Value,
            FirmTypeId = f.FirmTypeId,
            Kind = f.Kind,
            Name = f.Name,
            TileX = f.TileX,
            TileY = f.TileY,
            LayoutW = f.LayoutW,
            LayoutH = f.LayoutH,
            RetailFamily = f.RetailFamily,
            ExtractKind = f.ExtractKind,
            FactorySize = f.FactorySize,
            MonthlyExpense = f.MonthlyExpense,
            AutoApplyRd = f.AutoApplyRd,
            Units = f.Units.Select(UnitDto.From).ToList(),
            Links = f.Links.Select(l => new LinkDto { From = l.From.Value, To = l.To.Value }).ToList(),
            Inventory = f.Inventory.Select(i => new LotDto
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                Quality = i.Quality,
                UnitCost = i.UnitCost,
            }).ToList(),
        }).ToList(),
        Holdings = world.Holdings.Select(h => new HoldingDto
        {
            Owner = h.Owner.Value,
            Issuer = h.Issuer.Value,
            Shares = h.Shares,
        }).ToList(),
        News = world.News.TakeLast(100).Select(n => new NewsDto { Day = n.Day, Text = n.Text }).ToList(),
        Win = new WinDto { Won = world.Win.Won, Lost = world.Win.Lost, Message = world.Win.Message },
    };

    public static GameWorld FromDto(SaveDto dto)
    {
        var catalog = GameCatalog.LoadEmbedded();
        var playerDto = dto.Corporations.First(c => c.IsPlayer);
        var player = HydrateCorp(playerDto);
        var world = new GameWorld(catalog, player, dto.Seed)
        {
            Scenario = dto.Scenario,
            Day = dto.Day,
            Speed = dto.Speed,
            Paused = dto.Paused,
            SelectedCityName = dto.SelectedCityName,
            ScenarioTargetProfit = dto.ScenarioTargetProfit,
            ScenarioMaxDays = dto.ScenarioMaxDays,
        };
        world.Corporations.Clear();
        foreach (var c in dto.Corporations)
            world.Corporations.Add(c.IsPlayer ? player : HydrateCorp(c));

        foreach (var c in dto.Cities)
        {
            var city = new City
            {
                Id = new CityId(c.Id),
                Name = c.Name,
                Width = c.Width,
                Height = c.Height,
                SpendingLevel = c.SpendingLevel,
                SalaryLevel = c.SalaryLevel,
                Climate = c.Climate,
                Population = c.Population,
                Tiles = new CityTile[c.Width, c.Height],
            };
            for (var i = 0; i < c.Tiles.Count; i++)
            {
                var t = c.Tiles[i];
                city.Tiles[t.X, t.Y] = new CityTile
                {
                    Kind = t.Kind,
                    LandCost = t.LandCost,
                    FirmId = t.FirmId is { } fid ? new FirmId(fid) : null,
                };
            }
            world.Cities.Add(city);
        }

        foreach (var f in dto.Firms)
        {
            var firm = new Firm
            {
                Id = new FirmId(f.Id),
                Owner = new CorpId(f.Owner),
                CityId = new CityId(f.CityId),
                FirmTypeId = f.FirmTypeId,
                Kind = f.Kind,
                Name = f.Name,
                TileX = f.TileX,
                TileY = f.TileY,
                LayoutW = f.LayoutW,
                LayoutH = f.LayoutH,
                RetailFamily = f.RetailFamily,
                ExtractKind = f.ExtractKind,
                FactorySize = f.FactorySize,
                MonthlyExpense = f.MonthlyExpense,
                AutoApplyRd = f.AutoApplyRd,
            };
            foreach (var u in f.Units)
                firm.Units.Add(u.ToUnit());
            foreach (var l in f.Links)
                firm.Links.Add((new UnitId(l.From), new UnitId(l.To)));
            foreach (var i in f.Inventory)
                firm.Inventory.Add(new StockLot
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    Quality = i.Quality,
                    UnitCost = i.UnitCost,
                });
            world.Firms.Add(firm);
        }

        foreach (var h in dto.Holdings)
            world.Holdings.Add(new ShareHolding
            {
                Owner = new CorpId(h.Owner),
                Issuer = new CorpId(h.Issuer),
                Shares = h.Shares,
            });
        foreach (var n in dto.News)
            world.News.Add(new NewsItem { Day = n.Day, Text = n.Text });
        world.Win.Won = dto.Win.Won;
        world.Win.Lost = dto.Win.Lost;
        world.Win.Message = dto.Win.Message;
        return world;
    }

    private static Corporation HydrateCorp(CorpDto c)
    {
        var corp = new Corporation
        {
            Id = new CorpId(c.Id),
            Name = c.Name,
            IsPlayer = c.IsPlayer,
            IsAi = c.IsAi,
            Cash = c.Cash,
            OpeningCash = c.OpeningCash,
            BrandStrategy = c.BrandStrategy,
            SharesOutstanding = c.SharesOutstanding,
            SharePrice = c.SharePrice,
            DividendPerShare = c.DividendPerShare,
            LastYearProfit = c.LastYearProfit,
            AiAggressiveness = c.AiAggressiveness,
            Retired = c.Retired,
            PnlCursor = c.PnlCursor,
        };
        if (c.MonthlyPnl is { Length: > 0 })
            Array.Copy(c.MonthlyPnl, corp.MonthlyPnl, Math.Min(12, c.MonthlyPnl.Length));
        foreach (var (k, v) in c.Brands)
            corp.Brands[k] = new BrandState { Awareness = v.Awareness, Loyalty = v.Loyalty };
        foreach (var (k, v) in c.Tech)
            corp.Tech.ProductTech[k] = v;
        foreach (var l in c.Loans)
            corp.Loans.Add(new Loan { Borrower = corp.Id, Principal = l.Principal, MonthlyRate = l.MonthlyRate });
        corp.Hq.FinanceAutoDividend = c.Hq.FinanceAutoDividend;
        corp.Hq.MarketingAutoAds = c.Hq.MarketingAutoAds;
        corp.Hq.ImportPreferInternal = c.Hq.ImportPreferInternal;
        corp.Hq.RdAutoStart = c.Hq.RdAutoStart;
        return corp;
    }

    private static List<TileDto> FlattenTiles(City c)
    {
        var list = new List<TileDto>();
        for (var y = 0; y < c.Height; y++)
        for (var x = 0; x < c.Width; x++)
        {
            var t = c.Tiles[x, y];
            list.Add(new TileDto
            {
                X = x,
                Y = y,
                Kind = t.Kind,
                LandCost = t.LandCost,
                FirmId = t.FirmId?.Value,
            });
        }
        return list;
    }
}

internal sealed class SaveDto
{
    public ScenarioId Scenario { get; set; }
    public int Day { get; set; }
    public int Seed { get; set; }
    public int Speed { get; set; }
    public bool Paused { get; set; }
    public string? SelectedCityName { get; set; }
    public decimal ScenarioTargetProfit { get; set; }
    public int ScenarioMaxDays { get; set; }
    public List<CorpDto> Corporations { get; set; } = [];
    public List<CityDto> Cities { get; set; } = [];
    public List<FirmDto> Firms { get; set; } = [];
    public List<HoldingDto> Holdings { get; set; } = [];
    public List<NewsDto> News { get; set; } = [];
    public WinDto Win { get; set; } = new();
}

internal sealed class CorpDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsPlayer { get; set; }
    public bool IsAi { get; set; }
    public decimal Cash { get; set; }
    public decimal OpeningCash { get; set; }
    public BrandStrategy BrandStrategy { get; set; }
    public decimal SharesOutstanding { get; set; }
    public decimal SharePrice { get; set; }
    public decimal DividendPerShare { get; set; }
    public decimal LastYearProfit { get; set; }
    public double AiAggressiveness { get; set; }
    public bool Retired { get; set; }
    public Dictionary<string, BrandDto> Brands { get; set; } = new();
    public Dictionary<string, double> Tech { get; set; } = new();
    public List<LoanDto> Loans { get; set; } = [];
    public HqDto Hq { get; set; } = new();
    public decimal[] MonthlyPnl { get; set; } = [];
    public int PnlCursor { get; set; }
}

internal sealed class BrandDto
{
    public double Awareness { get; set; }
    public double Loyalty { get; set; }
}

internal sealed class LoanDto
{
    public decimal Principal { get; set; }
    public decimal MonthlyRate { get; set; }
}

internal sealed class HqDto
{
    public bool FinanceAutoDividend { get; set; }
    public bool MarketingAutoAds { get; set; }
    public bool ImportPreferInternal { get; set; }
    public bool RdAutoStart { get; set; }
}

internal sealed class CityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public double SpendingLevel { get; set; }
    public double SalaryLevel { get; set; }
    public EconomicClimate Climate { get; set; }
    public decimal Population { get; set; }
    public List<TileDto> Tiles { get; set; } = [];
}

internal sealed class TileDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public TileKind Kind { get; set; }
    public decimal LandCost { get; set; }
    public Guid? FirmId { get; set; }
}

internal sealed class FirmDto
{
    public Guid Id { get; set; }
    public Guid Owner { get; set; }
    public Guid CityId { get; set; }
    public string FirmTypeId { get; set; } = "";
    public FirmKind Kind { get; set; }
    public string Name { get; set; } = "";
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int LayoutW { get; set; }
    public int LayoutH { get; set; }
    public RetailFamily? RetailFamily { get; set; }
    public ExtractKind ExtractKind { get; set; }
    public int FactorySize { get; set; }
    public decimal MonthlyExpense { get; set; }
    public bool AutoApplyRd { get; set; }
    public List<UnitDto> Units { get; set; } = [];
    public List<LinkDto> Links { get; set; } = [];
    public List<LotDto> Inventory { get; set; } = [];
}

internal sealed class UnitDto
{
    public Guid Id { get; set; }
    public UnitKind Kind { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Level { get; set; }
    public double Training { get; set; }
    public bool PrivateLabel { get; set; }
    public string? PurchaseProductId { get; set; }
    public decimal PurchaseQtyTarget { get; set; }
    public bool PurchaseFromSeaport { get; set; }
    public Guid? PurchaseFromFirm { get; set; }
    public string? RecipeOutputId { get; set; }
    public decimal ProductionRate { get; set; }
    public string? SalesProductId { get; set; }
    public decimal SalesPrice { get; set; }
    public string? AdProductId { get; set; }
    public string? AdClass { get; set; }
    public decimal AdBudget { get; set; }
    public ExtractKind ExtractKind { get; set; }
    public string? ExtractProductId { get; set; }
    public decimal ExtractYield { get; set; }
    public string? RdTargetProductId { get; set; }
    public int RdMonthsRemaining { get; set; }
    public double RdProgress { get; set; }

    public static UnitDto From(FunctionalUnit u) => new()
    {
        Id = u.Id.Value,
        Kind = u.Kind,
        X = u.X,
        Y = u.Y,
        Level = u.Level,
        Training = u.Training,
        PrivateLabel = u.PrivateLabel,
        PurchaseProductId = u.PurchaseProductId,
        PurchaseQtyTarget = u.PurchaseQtyTarget,
        PurchaseFromSeaport = u.PurchaseFromSeaport,
        PurchaseFromFirm = u.PurchaseFromFirm?.Value,
        RecipeOutputId = u.RecipeOutputId,
        ProductionRate = u.ProductionRate,
        SalesProductId = u.SalesProductId,
        SalesPrice = u.SalesPrice,
        AdProductId = u.AdProductId,
        AdClass = u.AdClass,
        AdBudget = u.AdBudget,
        ExtractKind = u.ExtractKind,
        ExtractProductId = u.ExtractProductId,
        ExtractYield = u.ExtractYield,
        RdTargetProductId = u.RdTargetProductId,
        RdMonthsRemaining = u.RdMonthsRemaining,
        RdProgress = u.RdProgress,
    };

    public FunctionalUnit ToUnit() => new()
    {
        Id = new UnitId(Id),
        Kind = Kind,
        X = X,
        Y = Y,
        Level = Level,
        Training = Training,
        PrivateLabel = PrivateLabel,
        PurchaseProductId = PurchaseProductId,
        PurchaseQtyTarget = PurchaseQtyTarget,
        PurchaseFromSeaport = PurchaseFromSeaport,
        PurchaseFromFirm = PurchaseFromFirm is { } g ? new FirmId(g) : null,
        RecipeOutputId = RecipeOutputId,
        ProductionRate = ProductionRate,
        SalesProductId = SalesProductId,
        SalesPrice = SalesPrice,
        AdProductId = AdProductId,
        AdClass = AdClass,
        AdBudget = AdBudget,
        ExtractKind = ExtractKind,
        ExtractProductId = ExtractProductId,
        ExtractYield = ExtractYield,
        RdTargetProductId = RdTargetProductId,
        RdMonthsRemaining = RdMonthsRemaining,
        RdProgress = RdProgress,
    };
}

internal sealed class LinkDto
{
    public Guid From { get; set; }
    public Guid To { get; set; }
}

internal sealed class LotDto
{
    public string ProductId { get; set; } = "";
    public decimal Quantity { get; set; }
    public double Quality { get; set; }
    public decimal UnitCost { get; set; }
}

internal sealed class HoldingDto
{
    public Guid Owner { get; set; }
    public Guid Issuer { get; set; }
    public decimal Shares { get; set; }
}

internal sealed class NewsDto
{
    public int Day { get; set; }
    public string Text { get; set; } = "";
}

internal sealed class WinDto
{
    public bool Won { get; set; }
    public bool Lost { get; set; }
    public string Message { get; set; } = "";
}
