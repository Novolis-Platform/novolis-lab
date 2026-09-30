namespace CapitalistSimulator.Sim;

internal static class CompetitorAgent
{
    public static void Act(GameWorld world)
    {
        foreach (var corp in world.Corporations.Where(c => c.IsAi && !c.Retired))
        {
            // Ensure each AI has at least one store in a random city
            if (!world.FirmsOf(corp.Id).Any())
            {
                var city = world.Cities[world.Rng.Next(world.Cities.Count)];
                TryBuild(world, corp, city, "retail_conven", aggressiveness: corp.AiAggressiveness);
            }

            foreach (var firm in world.FirmsOf(corp.Id).Where(f => f.Kind == FirmKind.Retail))
            {
                foreach (var sales in firm.Units.Where(u => u.Kind == UnitKind.Sales))
                {
                    if (sales.SalesProductId is null)
                    {
                        var offer = world.Catalog.Seaport[world.Rng.Next(world.Catalog.Seaport.Count)];
                        var type = world.Catalog.FirmTypes[firm.FirmTypeId];
                        if (world.Catalog.Products.TryGetValue(offer.ProductId, out var prod)
                            && (type.AllowedClasses.Count == 0 || type.AllowedClasses.Contains(prod.Class)))
                        {
                            sales.SalesProductId = offer.ProductId;
                            sales.SalesPrice = prod.BasePrice * (decimal)(0.9 + world.Rng.NextDouble() * 0.3);
                        }
                    }
                    else if (world.Catalog.Products.TryGetValue(sales.SalesProductId, out var p))
                    {
                        // undercut / raise based on aggressiveness
                        var factor = 0.95 + (1 - corp.AiAggressiveness) * 0.15;
                        sales.SalesPrice = p.BasePrice * (decimal)factor;
                    }
                }

                foreach (var buy in firm.Units.Where(u => u.Kind == UnitKind.Purchasing))
                {
                    var sales = firm.Units.FirstOrDefault(u => u.Kind == UnitKind.Sales && u.SalesProductId is not null);
                    if (sales?.SalesProductId is null) continue;
                    buy.PurchaseProductId = sales.SalesProductId;
                    buy.PurchaseQtyTarget = 80 + (decimal)(corp.AiAggressiveness * 120);
                    buy.PurchaseFromSeaport = true;
                }

                foreach (var ad in firm.Units.Where(u => u.Kind == UnitKind.Advertising))
                {
                    ad.AdBudget = (decimal)(1000 + corp.AiAggressiveness * 4000);
                    ad.AdProductId = firm.Units.FirstOrDefault(u => u.Kind == UnitKind.Sales)?.SalesProductId;
                }
            }

            if (corp.AiAggressiveness > 0.6 && world.Rng.NextDouble() < 0.15
                && world.FirmsOf(corp.Id).Count() < 4)
            {
                var city = world.Cities[world.Rng.Next(world.Cities.Count)];
                TryBuild(world, corp, city, "retail_super", corp.AiAggressiveness);
            }
        }
    }

    private static void TryBuild(GameWorld world, Corporation corp, City city, string typeId, double aggressiveness)
    {
        if (!world.Catalog.FirmTypes.TryGetValue(typeId, out var type)) return;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var x = world.Rng.Next(0, Math.Max(1, city.Width - type.Width));
            var y = world.Rng.Next(0, Math.Max(1, city.Height - type.Height));
            var ok = true;
            for (var yy = y; yy < y + type.Height && ok; yy++)
            for (var xx = x; xx < x + type.Width && ok; xx++)
            {
                var t = city.Tiles[xx, yy];
                if (t.Kind != TileKind.Buildable || t.FirmId is not null) ok = false;
            }
            if (!ok) continue;

            var land = 0m;
            for (var yy = y; yy < y + type.Height; yy++)
            for (var xx = x; xx < x + type.Width; xx++)
                land += city.Tiles[xx, yy].LandCost;
            var cost = type.SetupCost + land;
            if (corp.Cash < cost) return;

            corp.Cash -= cost;
            var firm = new Firm
            {
                Owner = corp.Id,
                CityId = city.Id,
                FirmTypeId = type.Id,
                Kind = type.Kind,
                Name = $"{corp.Name} {type.Name}",
                TileX = x,
                TileY = y,
                LayoutW = type.LayoutW,
                LayoutH = type.LayoutH,
                RetailFamily = type.RetailFamily,
                MonthlyExpense = type.MonthlyCost,
            };
            firm.Units.Add(new FunctionalUnit { Kind = UnitKind.Purchasing, X = 0, Y = 0 });
            firm.Units.Add(new FunctionalUnit { Kind = UnitKind.Sales, X = 1, Y = 0 });
            firm.Units.Add(new FunctionalUnit { Kind = UnitKind.Sales, X = 2, Y = 0 });
            firm.Units.Add(new FunctionalUnit { Kind = UnitKind.Advertising, X = 0, Y = 1 });
            LinkRules.AutoLink(firm);
            for (var yy = y; yy < y + type.Height; yy++)
            for (var xx = x; xx < x + type.Width; xx++)
                city.Tiles[xx, yy].FirmId = firm.Id;
            world.Firms.Add(firm);
            world.AddNews($"{corp.Name} built {type.Name} in {city.Name}");
            return;
        }
        _ = aggressiveness;
    }
}
