namespace CapitalistSimulator.Sim;

internal static class WorldFactory
{
    public static GameWorld Create(
        ScenarioId scenario = ScenarioId.Sandbox,
        decimal startingCash = 2_000_000,
        int aiCount = 2,
        double aiAggressiveness = 0.55,
        int seed = 42)
    {
        var catalog = GameCatalog.LoadEmbedded();
        var player = new Corporation
        {
            Name = "Player Corp",
            IsPlayer = true,
            Cash = startingCash,
            OpeningCash = startingCash,
            SharePrice = 25,
            SharesOutstanding = 1_000_000,
        };
        // Player owns all their shares initially
        var world = new GameWorld(catalog, player, seed)
        {
            Scenario = scenario,
            Paused = true,
            Speed = 0,
        };
        world.Holdings.Add(new ShareHolding
        {
            Owner = player.Id,
            Issuer = player.Id,
            Shares = player.SharesOutstanding,
        });

        world.Cities.Add(BuildCity("Metropolis", 16, 12, seed));
        world.Cities.Add(BuildCity("Harbor Bay", 14, 10, seed + 1));
        world.Cities.Add(BuildCity("Midlands", 14, 10, seed + 2));
        world.SelectedCityName = world.Cities[0].Name;

        var aiNames = new[] { "Capital Corp", "King Industries", "Summit Retail", "Nova Goods" };
        for (var i = 0; i < aiCount; i++)
        {
            var ai = new Corporation
            {
                Name = aiNames[i % aiNames.Length] + (i >= aiNames.Length ? $" {i}" : ""),
                IsAi = true,
                Cash = startingCash * (decimal)(0.6 + aiAggressiveness * 0.5),
                OpeningCash = startingCash,
                AiAggressiveness = aiAggressiveness,
                SharePrice = 12 + i,
                SharesOutstanding = 800_000,
            };
            world.Corporations.Add(ai);
            world.Holdings.Add(new ShareHolding
            {
                Owner = ai.Id,
                Issuer = ai.Id,
                Shares = ai.SharesOutstanding * 0.7m,
            });
        }

        switch (scenario)
        {
            case ScenarioId.RetailProfit:
                // Trailing 12-month P&L (not EMA). Achievable with ~2 profitable stores.
                world.ScenarioTargetProfit = 40_000;
                world.ScenarioMaxDays = 365 * 15;
                world.AddNews("Goal: 2+ stores and $40k trailing-year profit.");
                break;
            case ScenarioId.WineDominance:
                world.ScenarioMaxDays = 365 * 25;
                world.AddNews("Goal: vertical grapes → wine → retail; hold majority wine share.");
                break;
            default:
                world.ScenarioMaxDays = 365 * 50;
                world.AddNews("Sandbox — grow your empire.");
                break;
        }

        // Seed AI presence
        CompetitorAgent.Act(world);
        return world;
    }

    private static City BuildCity(string name, int w, int h, int seed)
    {
        var rng = new Random(seed);
        var city = new City
        {
            Name = name,
            Width = w,
            Height = h,
            Tiles = new CityTile[w, h],
            Population = 80_000 + rng.Next(0, 80_000),
            SpendingLevel = 0.9 + rng.NextDouble() * 0.3,
            Climate = EconomicClimate.Stable,
        };
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            city.Tiles[x, y] = new CityTile
            {
                Kind = TileKind.Buildable,
                LandCost = 3000 + rng.Next(0, 8000) + (Math.Abs(x - w / 2) + Math.Abs(y - h / 2)) * 200,
            };
        }

        // Roads
        for (var x = 0; x < w; x++)
            city.Tiles[x, h / 2].Kind = TileKind.Road;
        for (var y = 0; y < h; y++)
            city.Tiles[w / 2, y].Kind = TileKind.Road;

        // Seaport strip
        for (var x = 0; x < Math.Min(3, w); x++)
            city.Tiles[x, 0].Kind = TileKind.Seaport;

        city.Tiles[w - 2, h - 2].Kind = TileKind.Bank;
        city.Tiles[w - 1, h - 2].Kind = TileKind.StockExchange;

        return city;
    }
}
