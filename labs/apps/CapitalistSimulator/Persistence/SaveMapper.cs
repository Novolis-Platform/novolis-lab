using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

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
