namespace CapitalistSimulator.Sim;

internal sealed class GameWorld
{
    public GameCatalog Catalog { get; }
    public ScenarioId Scenario { get; set; } = ScenarioId.Sandbox;
    public int Day { get; set; } = 1;
    public int Year => 2000 + (Day - 1) / 365;
    public int MonthOfYear => ((Day - 1) % 365) / 30 + 1;
    public int Speed { get; set; } = 1;
    public bool Paused { get; set; } = true;
    public int Seed { get; }
    public Random Rng { get; }

    public Corporation Player { get; }
    public List<Corporation> Corporations { get; } = [];
    public List<City> Cities { get; } = [];
    public List<Firm> Firms { get; } = [];
    public List<ShareHolding> Holdings { get; } = [];
    public List<NewsItem> News { get; } = [];
    public List<MarketShareSnap> LastMonthSales { get; } = [];
    public WinState Win { get; } = new();
    public decimal ScenarioTargetProfit { get; set; } = 5_000_000;
    public int ScenarioMaxDays { get; set; } = 365 * 20;
    public string? SelectedCityName { get; set; }

    public GameWorld(GameCatalog catalog, Corporation player, int seed)
    {
        Catalog = catalog;
        Player = player;
        Seed = seed;
        Rng = new Random(seed);
        Corporations.Add(player);
    }

    public City? FindCity(CityId id) => Cities.FirstOrDefault(c => c.Id.Equals(id));
    public City? FindCityByName(string name) =>
        Cities.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    public Firm? FindFirm(FirmId id) => Firms.FirstOrDefault(f => f.Id.Equals(id));

    public Corporation? FindCorp(CorpId id) => Corporations.FirstOrDefault(c => c.Id.Equals(id));

    public IEnumerable<Firm> FirmsOf(CorpId corp) => Firms.Where(f => f.Owner.Equals(corp));

    public void AddNews(string text)
    {
        News.Add(new NewsItem { Day = Day, Text = text });
        if (News.Count > 200)
            News.RemoveRange(0, News.Count - 200);
    }

    public StockLot? FindLot(Firm firm, string productId) =>
        firm.Inventory.FirstOrDefault(l => string.Equals(l.ProductId, productId, StringComparison.OrdinalIgnoreCase));

    public StockLot GetOrCreateLot(Firm firm, string productId, double quality = 0.5)
    {
        var lot = FindLot(firm, productId);
        if (lot is not null)
            return lot;
        lot = new StockLot { ProductId = productId, Quality = quality };
        firm.Inventory.Add(lot);
        return lot;
    }

    public decimal PlayerNetWorth()
    {
        var assets = Player.Cash;
        foreach (var h in Holdings.Where(h => h.Owner.Equals(Player.Id)))
        {
            var issuer = FindCorp(h.Issuer);
            if (issuer is not null)
                assets += h.Shares * issuer.SharePrice;
        }
        foreach (var firm in FirmsOf(Player.Id))
        {
            foreach (var lot in firm.Inventory)
                assets += lot.Quantity * lot.UnitCost;
        }
        var debt = Player.Loans.Sum(l => l.Principal);
        return assets - debt;
    }
}
