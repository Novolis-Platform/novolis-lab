namespace CapitalistSimulator.Sim;

/// <summary>Puts the player into a running Cap2-style retail loop (tut01).</summary>
internal static class StarterBootstrap
{
    public static Firm? EnsureStarterRetail(GameWorld world)
    {
        var existing = world.FirmsOf(world.Player.Id).FirstOrDefault(f => f.Kind == FirmKind.Retail);
        if (existing is not null)
            return existing;

        var proc = new CommandProcessor(world);
        var city = world.Cities[0];
        world.SelectedCityName = city.Name;

        // Prefer a clear buildable patch near center-left (avoid road / seaport).
        var placed = false;
        for (var y = 2; y < city.Height - 3 && !placed; y++)
        for (var x = 1; x < city.Width - 4 && !placed; x++)
        {
            var r = proc.Apply(new BuildFirmCommand(city.Name, "retail_super", x, y, "Corner Market"));
            if (r.Ok) placed = true;
        }

        if (!placed)
        {
            world.AddNews("Could not auto-place starter store — pick a green tile and Build.");
            return null;
        }

        var firm = world.FirmsOf(world.Player.Id).First(f => f.Kind == FirmKind.Retail);
        ConfigureStockedSupermarket(world, proc, firm);
        world.AddNews("Starter: Corner Market is stocked with bread & milk from the seaport. Press Advance month.");
        world.Player.Brands["corporate"] = new BrandState { Awareness = 0.25, Loyalty = 0.15 };
        world.Player.Brands["Food"] = new BrandState { Awareness = 0.2, Loyalty = 0.1 };
        return firm;
    }

    public static void ConfigureStockedSupermarket(GameWorld world, CommandProcessor proc, Firm firm)
    {
        while (firm.Units.Count(u => u.Kind == UnitKind.Purchasing) < 3)
        {
            var added = false;
            for (var y = 0; y < firm.LayoutH && !added; y++)
            for (var x = 0; x < firm.LayoutW && !added; x++)
            {
                if (firm.Units.Any(u => u.X == x && u.Y == y)) continue;
                var r = proc.Apply(new PlaceUnitCommand(firm.Id, UnitKind.Purchasing, x, y));
                added = r.Ok;
            }
            if (!added) break;
        }

        var buys = firm.Units.Where(u => u.Kind == UnitKind.Purchasing).ToList();
        var sales = firm.Units.Where(u => u.Kind == UnitKind.Sales).ToList();

        // Targets are caps; MonthTick further limits to recent sell-through.
        if (buys.Count > 0)
            proc.Apply(new ConfigurePurchasingCommand(firm.Id, buys[0].Id, "bread", 900, true, null, false));
        if (buys.Count > 1)
            proc.Apply(new ConfigurePurchasingCommand(firm.Id, buys[1].Id, "milk", 700, true, null, false));
        if (buys.Count > 2)
            proc.Apply(new ConfigurePurchasingCommand(firm.Id, buys[2].Id, "soda", 500, true, null, false));
        // ~1.8–1.9× catalog base — enough gross margin for 2 stores to clear $40k trailing-year.
        if (sales.Count > 0)
            proc.Apply(new ConfigureSalesCommand(firm.Id, sales[0].Id, "bread", 5.60m));
        if (sales.Count > 1)
            proc.Apply(new ConfigureSalesCommand(firm.Id, sales[1].Id, "milk", 7.60m));
        if (sales.Count > 2)
            proc.Apply(new ConfigureSalesCommand(firm.Id, sales[2].Id, "soda", 5.60m));

        var ad = firm.Units.FirstOrDefault(u => u.Kind == UnitKind.Advertising);
        if (ad is not null)
            proc.Apply(new ConfigureAdvertisingCommand(firm.Id, ad.Id, "bread", "Food", 200));

        proc.Apply(new AutoLinkCommand(firm.Id));

        // Prime shelves so month 1 sells immediately (don't wait a purchase cycle with empty shelves).
        SeedShelf(world, firm, "bread", 1600, 0.55, 2.5m);
        SeedShelf(world, firm, "milk", 1200, 0.55, 3.5m);
        SeedShelf(world, firm, "soda", 800, 0.5, 2.2m);
    }

    private static void SeedShelf(GameWorld world, Firm firm, string productId, decimal qty, double quality, decimal unitCost)
    {
        var lot = world.GetOrCreateLot(firm, productId, quality);
        lot.Quantity = Math.Max(lot.Quantity, qty);
        lot.Quality = quality;
        lot.UnitCost = unitCost;
    }
}
