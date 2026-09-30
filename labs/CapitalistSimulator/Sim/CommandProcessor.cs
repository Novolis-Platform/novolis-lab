namespace CapitalistSimulator.Sim;

internal sealed class CommandProcessor
{
    private readonly GameWorld _world;

    public CommandProcessor(GameWorld world) => _world = world;

    public CommandResult Apply(PlayerCommand cmd) => cmd switch
    {
        NewGameCommand n => ApplyNewGame(n),
        SetSpeedCommand s => ApplySpeed(s),
        SetPausedCommand p => SetPaused(p),
        AdvanceDaysCommand a => AdvanceDays(a.Days),
        SelectCityCommand c => SelectCity(c.CityName),
        BuildFirmCommand b => BuildFirm(b),
        DemolishFirmCommand d => Demolish(d.FirmId),
        PlaceUnitCommand u => PlaceUnit(u),
        RemoveUnitCommand r => RemoveUnit(r),
        SetLinkCommand l => SetLink(l),
        ClearLinksCommand cl => ClearLinks(cl.FirmId),
        AutoLinkCommand al => AutoLink(al.FirmId),
        SetTrainingCommand t => SetTraining(t),
        ConfigurePurchasingCommand p => ConfigurePurchasing(p),
        ConfigureManufacturingCommand m => ConfigureMfg(m),
        ConfigureSalesCommand s => ConfigureSales(s),
        ConfigureAdvertisingCommand a => ConfigureAd(a),
        ConfigureExtractCommand e => ConfigureExtract(e),
        StartRdCommand rd => StartRd(rd),
        CancelRdCommand cr => CancelRd(cr),
        SetAutoApplyRdCommand ar => SetAutoRd(ar),
        SetBrandStrategyCommand bs => SetBrand(bs),
        BorrowCommand b => Borrow(b.Amount),
        RepayCommand r => Repay(r.Amount),
        BuySharesCommand buy => BuyShares(buy),
        SellSharesCommand sell => SellShares(sell),
        IssueSharesCommand iss => IssueShares(iss),
        SetDividendCommand div => SetDividend(div),
        SetHqFinanceAutoCommand hq => SetHq(hq),
        SetHqMarketingAutoCommand hq => SetHq(hq),
        SetHqImportPreferInternalCommand hq => SetHq(hq),
        SetHqRdAutoCommand hq => SetHq(hq),
        RetireCommand => Retire(),
        AbsorbCorpCommand abs => Absorb(abs.Target),
        _ => CommandResult.Fail("Unknown command"),
    };

    private CommandResult SetPaused(SetPausedCommand p)
    {
        _world.Paused = p.Paused;
        return CommandResult.Success(p.Paused ? "Paused" : "Running");
    }

    private CommandResult SetBrand(SetBrandStrategyCommand bs)
    {
        _world.Player.BrandStrategy = bs.Strategy;
        return CommandResult.Success($"Brand strategy: {bs.Strategy}");
    }

    private CommandResult SetDividend(SetDividendCommand div)
    {
        _world.Player.DividendPerShare = Math.Max(0, div.PerShare);
        return CommandResult.Success();
    }

    private CommandResult SetHq(SetHqFinanceAutoCommand hq)
    {
        _world.Player.Hq.FinanceAutoDividend = hq.On;
        return CommandResult.Success();
    }

    private CommandResult SetHq(SetHqMarketingAutoCommand hq)
    {
        _world.Player.Hq.MarketingAutoAds = hq.On;
        return CommandResult.Success();
    }

    private CommandResult SetHq(SetHqImportPreferInternalCommand hq)
    {
        _world.Player.Hq.ImportPreferInternal = hq.On;
        return CommandResult.Success();
    }

    private CommandResult SetHq(SetHqRdAutoCommand hq)
    {
        _world.Player.Hq.RdAutoStart = hq.On;
        return CommandResult.Success();
    }

    private CommandResult ApplyNewGame(NewGameCommand n)
    {
        // New games are created via WorldFactory; this is a no-op marker for CLI.
        return CommandResult.Success($"Scenario {n.Scenario}");
    }

    private CommandResult ApplySpeed(SetSpeedCommand s)
    {
        _world.Speed = Math.Clamp(s.Speed, 0, 5);
        _world.Paused = _world.Speed == 0;
        return CommandResult.Success($"Speed {_world.Speed}");
    }

    private CommandResult AdvanceDays(int days)
    {
        if (days <= 0) return CommandResult.Fail("Days must be > 0");
        var tick = new MonthTick(_world);
        for (var i = 0; i < days; i++)
        {
            _world.Day++;
            if ((_world.Day - 1) % 30 == 0)
                tick.RunMonth();
            if (_world.Win.Won || _world.Win.Lost)
                break;
        }
        return CommandResult.Success($"Advanced to day {_world.Day}");
    }

    private CommandResult SelectCity(string name)
    {
        var city = _world.FindCityByName(name);
        if (city is null) return CommandResult.Fail($"City not found: {name}");
        _world.SelectedCityName = city.Name;
        return CommandResult.Success($"Selected {city.Name}");
    }

    private CommandResult BuildFirm(BuildFirmCommand b)
    {
        var city = _world.FindCityByName(b.CityName);
        if (city is null) return CommandResult.Fail($"City not found: {b.CityName}");
        if (!_world.Catalog.FirmTypes.TryGetValue(b.FirmTypeId, out var type))
            return CommandResult.Fail($"Unknown firm type: {b.FirmTypeId}");

        if (b.TileX < 0 || b.TileY < 0 || b.TileX + type.Width > city.Width || b.TileY + type.Height > city.Height)
            return CommandResult.Fail("Out of bounds");

        for (var y = b.TileY; y < b.TileY + type.Height; y++)
        for (var x = b.TileX; x < b.TileX + type.Width; x++)
        {
            var tile = city.Tiles[x, y];
            if (tile.Kind != TileKind.Buildable || tile.FirmId is not null)
                return CommandResult.Fail($"Tile ({x},{y}) not buildable");
        }

        var land = 0m;
        for (var y = b.TileY; y < b.TileY + type.Height; y++)
        for (var x = b.TileX; x < b.TileX + type.Width; x++)
            land += city.Tiles[x, y].LandCost;

        var cost = type.SetupCost + land;
        if (_world.Player.Cash < cost)
            return CommandResult.Fail($"Need ${cost:N0}, have ${_world.Player.Cash:N0}");

        if (type.Kind == FirmKind.Hq && _world.FirmsOf(_world.Player.Id).Any(f => f.Kind == FirmKind.Hq))
            return CommandResult.Fail("HQ already exists");

        _world.Player.Cash -= cost;
        var firm = new Firm
        {
            Owner = _world.Player.Id,
            CityId = city.Id,
            FirmTypeId = type.Id,
            Kind = type.Kind,
            Name = b.Name ?? type.Name,
            TileX = b.TileX,
            TileY = b.TileY,
            LayoutW = type.LayoutW,
            LayoutH = type.LayoutH,
            RetailFamily = type.RetailFamily,
            ExtractKind = type.ExtractKind ?? ExtractKind.None,
            FactorySize = type.Size,
            MonthlyExpense = type.MonthlyCost,
        };

        SeedDefaultUnits(firm, type);
        LinkRules.AutoLink(firm);

        for (var y = b.TileY; y < b.TileY + type.Height; y++)
        for (var x = b.TileX; x < b.TileX + type.Width; x++)
            city.Tiles[x, y].FirmId = firm.Id;

        _world.Firms.Add(firm);
        _world.AddNews($"Built {firm.Name} in {city.Name} for ${cost:N0}");
        return CommandResult.Success($"Built {firm.Name} ({firm.Id})");
    }

    private static void SeedDefaultUnits(Firm firm, FirmTypeDef type)
    {
        void Add(UnitKind kind, int x, int y)
        {
            firm.Units.Add(new FunctionalUnit { Kind = kind, X = x, Y = y });
        }

        switch (type.Kind)
        {
            case FirmKind.Retail:
                Add(UnitKind.Purchasing, 0, 0);
                Add(UnitKind.Inventory, 1, 0);
                Add(UnitKind.Sales, 2, 0);
                Add(UnitKind.Sales, 3, 0);
                Add(UnitKind.Sales, 2, 1);
                Add(UnitKind.Sales, 3, 1);
                Add(UnitKind.Advertising, 0, 1);
                break;
            case FirmKind.Factory:
                Add(UnitKind.Purchasing, 0, 0);
                Add(UnitKind.Manufacturing, 1, 0);
                Add(UnitKind.Inventory, 2, 0);
                Add(UnitKind.Sales, 3, 0);
                break;
            case FirmKind.Farm:
                Add(UnitKind.Extract, 0, 0);
                Add(UnitKind.Inventory, 1, 0);
                Add(UnitKind.Sales, 2, 0);
                firm.Units[0].ExtractKind = ExtractKind.Crop;
                firm.Units[0].ExtractProductId = "grapes";
                break;
            case FirmKind.Extract:
                Add(UnitKind.Extract, 0, 0);
                Add(UnitKind.Sales, 1, 0);
                firm.Units[0].ExtractKind = type.ExtractKind ?? ExtractKind.Mine;
                firm.Units[0].ExtractProductId = type.ExtractKind switch
                {
                    ExtractKind.Forest => "timber",
                    ExtractKind.Oil => "oil",
                    _ => "iron",
                };
                break;
            case FirmKind.Rd:
                Add(UnitKind.Rd, 0, 0);
                Add(UnitKind.Rd, 1, 0);
                break;
            case FirmKind.Hq:
                Add(UnitKind.Sales, 0, 0); // placeholder sales unit
                break;
        }
    }

    private CommandResult Demolish(FirmId id)
    {
        var firm = _world.FindFirm(id);
        if (firm is null) return CommandResult.Fail("Firm not found");
        if (!firm.Owner.Equals(_world.Player.Id)) return CommandResult.Fail("Not your firm");
        var city = _world.FindCity(firm.CityId);
        if (city is not null)
        {
            for (var y = 0; y < city.Height; y++)
            for (var x = 0; x < city.Width; x++)
            {
                if (city.Tiles[x, y].FirmId?.Equals(id) == true)
                    city.Tiles[x, y].FirmId = null;
            }
        }
        _world.Firms.Remove(firm);
        _world.AddNews($"Demolished {firm.Name}");
        return CommandResult.Success();
    }

    private CommandResult PlaceUnit(PlaceUnitCommand u)
    {
        var firm = RequirePlayerFirm(u.FirmId, out var err);
        if (firm is null) return err!;
        if (u.X < 0 || u.Y < 0 || u.X >= firm.LayoutW || u.Y >= firm.LayoutH)
            return CommandResult.Fail("Layout out of bounds");
        if (firm.Units.Any(x => x.X == u.X && x.Y == u.Y))
            return CommandResult.Fail("Cell occupied");
        if (firm.Kind == FirmKind.Retail && u.Kind == UnitKind.Sales && firm.Units.Count(x => x.Kind == UnitKind.Sales) >= 4)
            return CommandResult.Fail("Retail has max 4 sales slots");
        firm.Units.Add(new FunctionalUnit { Kind = u.Kind, X = u.X, Y = u.Y });
        return CommandResult.Success();
    }

    private CommandResult RemoveUnit(RemoveUnitCommand r)
    {
        var firm = RequirePlayerFirm(r.FirmId, out var err);
        if (firm is null) return err!;
        var unit = firm.Units.FirstOrDefault(u => u.Id.Equals(r.UnitId));
        if (unit is null) return CommandResult.Fail("Unit not found");
        firm.Units.Remove(unit);
        firm.Links.RemoveAll(l => l.From.Equals(r.UnitId) || l.To.Equals(r.UnitId));
        return CommandResult.Success();
    }

    private CommandResult SetLink(SetLinkCommand l)
    {
        var firm = RequirePlayerFirm(l.FirmId, out var err);
        if (firm is null) return err!;
        var from = firm.Units.FirstOrDefault(u => u.Id.Equals(l.From));
        var to = firm.Units.FirstOrDefault(u => u.Id.Equals(l.To));
        if (from is null || to is null) return CommandResult.Fail("Unit missing");
        if (!LinkRules.CanLink(from.Kind, to.Kind)) return CommandResult.Fail("Invalid link");
        firm.Links.RemoveAll(x => x.From.Equals(l.From) && x.To.Equals(l.To));
        firm.Links.Add((l.From, l.To));
        return CommandResult.Success();
    }

    private CommandResult ClearLinks(FirmId id)
    {
        var firm = RequirePlayerFirm(id, out var err);
        if (firm is null) return err!;
        firm.Links.Clear();
        return CommandResult.Success();
    }

    private CommandResult AutoLink(FirmId id)
    {
        var firm = RequirePlayerFirm(id, out var err);
        if (firm is null) return err!;
        LinkRules.AutoLink(firm);
        return CommandResult.Success($"Links: {firm.Links.Count}");
    }

    private CommandResult SetTraining(SetTrainingCommand t)
    {
        var unit = RequireUnit(t.FirmId, t.UnitId, out var err);
        if (unit is null) return err!;
        unit.Training = Math.Clamp(t.Training, 0, 1);
        return CommandResult.Success();
    }

    private CommandResult ConfigurePurchasing(ConfigurePurchasingCommand p)
    {
        var unit = RequireUnit(p.FirmId, p.UnitId, out var err);
        if (unit is null) return err!;
        if (unit.Kind != UnitKind.Purchasing) return CommandResult.Fail("Not purchasing");
        if (!_world.Catalog.Products.ContainsKey(p.ProductId)) return CommandResult.Fail("Unknown product");
        unit.PurchaseProductId = p.ProductId;
        unit.PurchaseQtyTarget = Math.Max(0, p.QtyTarget);
        unit.PurchaseFromSeaport = p.FromSeaport;
        unit.PurchaseFromFirm = p.FromFirm;
        unit.PrivateLabel = p.PrivateLabel;
        return CommandResult.Success();
    }

    private CommandResult ConfigureMfg(ConfigureManufacturingCommand m)
    {
        var unit = RequireUnit(m.FirmId, m.UnitId, out var err);
        if (unit is null) return err!;
        if (unit.Kind != UnitKind.Manufacturing) return CommandResult.Fail("Not manufacturing");
        if (!_world.Catalog.RecipesByOutput.ContainsKey(m.RecipeOutputId))
            return CommandResult.Fail("Unknown recipe");
        unit.RecipeOutputId = m.RecipeOutputId;
        unit.ProductionRate = Math.Max(0, m.ProductionRate);
        return CommandResult.Success();
    }

    private CommandResult ConfigureSales(ConfigureSalesCommand s)
    {
        var firm = RequirePlayerFirm(s.FirmId, out var err);
        if (firm is null) return err!;
        var unit = firm.Units.FirstOrDefault(u => u.Id.Equals(s.UnitId));
        if (unit is null || unit.Kind != UnitKind.Sales) return CommandResult.Fail("Not sales");
        if (!_world.Catalog.Products.TryGetValue(s.ProductId, out var prod))
            return CommandResult.Fail("Unknown product");
        if (firm.Kind == FirmKind.Retail && firm.RetailFamily is not null)
        {
            var type = _world.Catalog.FirmTypes[firm.FirmTypeId];
            if (type.AllowedClasses.Count > 0 && !type.AllowedClasses.Contains(prod.Class))
                return CommandResult.Fail($"{prod.Name} not sold in {type.Name}");
        }
        unit.SalesProductId = s.ProductId;
        unit.SalesPrice = Math.Max(0.01m, s.Price);
        return CommandResult.Success();
    }

    private CommandResult ConfigureAd(ConfigureAdvertisingCommand a)
    {
        var unit = RequireUnit(a.FirmId, a.UnitId, out var err);
        if (unit is null) return err!;
        if (unit.Kind != UnitKind.Advertising) return CommandResult.Fail("Not advertising");
        unit.AdProductId = a.ProductId;
        unit.AdClass = a.ProductClass;
        unit.AdBudget = Math.Max(0, a.Budget);
        return CommandResult.Success();
    }

    private CommandResult ConfigureExtract(ConfigureExtractCommand e)
    {
        var unit = RequireUnit(e.FirmId, e.UnitId, out var err);
        if (unit is null) return err!;
        if (unit.Kind != UnitKind.Extract) return CommandResult.Fail("Not extract");
        unit.ExtractKind = e.Kind;
        unit.ExtractProductId = e.ProductId;
        unit.ExtractYield = Math.Max(0, e.Yield);
        return CommandResult.Success();
    }

    private CommandResult StartRd(StartRdCommand rd)
    {
        var unit = RequireUnit(rd.FirmId, rd.UnitId, out var err);
        if (unit is null) return err!;
        if (unit.Kind != UnitKind.Rd) return CommandResult.Fail("Not R&D");
        if (!_world.Catalog.Products.ContainsKey(rd.ProductId)) return CommandResult.Fail("Unknown product");
        unit.RdTargetProductId = rd.ProductId;
        unit.RdMonthsRemaining = Math.Clamp(rd.Months, 1, 36);
        unit.RdProgress = 0;
        return CommandResult.Success($"R&D started on {rd.ProductId}");
    }

    private CommandResult CancelRd(CancelRdCommand cr)
    {
        var unit = RequireUnit(cr.FirmId, cr.UnitId, out var err);
        if (unit is null) return err!;
        unit.RdTargetProductId = null;
        unit.RdMonthsRemaining = 0;
        unit.RdProgress = 0;
        return CommandResult.Success();
    }

    private CommandResult SetAutoRd(SetAutoApplyRdCommand ar)
    {
        var firm = RequirePlayerFirm(ar.FirmId, out var err);
        if (firm is null) return err!;
        firm.AutoApplyRd = ar.Auto;
        return CommandResult.Success();
    }

    private CommandResult Borrow(decimal amount)
    {
        if (amount <= 0) return CommandResult.Fail("Amount must be > 0");
        var max = Math.Max(50_000m, _world.PlayerNetWorth() * 0.5m);
        var outstanding = _world.Player.Loans.Sum(l => l.Principal);
        if (outstanding + amount > max)
            return CommandResult.Fail($"Credit limit ${max:N0}");
        _world.Player.Loans.Add(new Loan { Borrower = _world.Player.Id, Principal = amount });
        _world.Player.Cash += amount;
        _world.AddNews($"Borrowed ${amount:N0}");
        return CommandResult.Success();
    }

    private CommandResult Repay(decimal amount)
    {
        if (amount <= 0) return CommandResult.Fail("Amount must be > 0");
        amount = Math.Min(amount, _world.Player.Cash);
        var remaining = amount;
        foreach (var loan in _world.Player.Loans.ToList())
        {
            var pay = Math.Min(remaining, loan.Principal);
            loan.Principal -= pay;
            _world.Player.Cash -= pay;
            remaining -= pay;
            if (loan.Principal <= 0)
                _world.Player.Loans.Remove(loan);
            if (remaining <= 0) break;
        }
        return CommandResult.Success($"Repaid ${amount - remaining:N0}");
    }

    private CommandResult BuyShares(BuySharesCommand buy)
    {
        var issuer = _world.FindCorp(buy.Issuer);
        if (issuer is null) return CommandResult.Fail("Issuer not found");
        if (buy.Shares <= 0) return CommandResult.Fail("Shares must be > 0");
        var cost = buy.Shares * issuer.SharePrice;
        if (_world.Player.Cash < cost) return CommandResult.Fail("Insufficient cash");
        if (buy.Shares > issuer.SharesOutstanding)
            return CommandResult.Fail("Not enough shares");
        _world.Player.Cash -= cost;
        var holding = _world.Holdings.FirstOrDefault(h => h.Owner.Equals(_world.Player.Id) && h.Issuer.Equals(issuer.Id));
        if (holding is null)
        {
            holding = new ShareHolding { Owner = _world.Player.Id, Issuer = issuer.Id };
            _world.Holdings.Add(holding);
        }
        holding.Shares += buy.Shares;
        issuer.Cash += cost * 0.05m; // tiny primary-ish flow
        _world.AddNews($"Bought {buy.Shares:N0} shares of {issuer.Name} @ ${issuer.SharePrice:N2}");
        return CommandResult.Success();
    }

    private CommandResult SellShares(SellSharesCommand sell)
    {
        var holding = _world.Holdings.FirstOrDefault(h => h.Owner.Equals(_world.Player.Id) && h.Issuer.Equals(sell.Issuer));
        if (holding is null || holding.Shares < sell.Shares)
            return CommandResult.Fail("Not enough shares");
        var issuer = _world.FindCorp(sell.Issuer);
        if (issuer is null) return CommandResult.Fail("Issuer not found");
        holding.Shares -= sell.Shares;
        _world.Player.Cash += sell.Shares * issuer.SharePrice;
        if (holding.Shares <= 0) _world.Holdings.Remove(holding);
        return CommandResult.Success();
    }

    private CommandResult IssueShares(IssueSharesCommand iss)
    {
        if (iss.Shares <= 0) return CommandResult.Fail("Shares must be > 0");
        var price = iss.Price > 0 ? iss.Price : _world.Player.SharePrice;
        _world.Player.SharesOutstanding += iss.Shares;
        _world.Player.Cash += iss.Shares * price;
        _world.Player.SharePrice = price * 0.98m;
        var publicHolding = new ShareHolding
        {
            Owner = CorpId.New(), // anonymous float bucket represented as orphan — track via outstanding
            Issuer = _world.Player.Id,
            Shares = 0,
        };
        _ = publicHolding;
        _world.AddNews($"Issued {iss.Shares:N0} shares @ ${price:N2}");
        return CommandResult.Success();
    }

    private CommandResult Retire()
    {
        _world.Player.Retired = true;
        _world.Win.Lost = true;
        _world.Win.Message = "Retired from the market.";
        _world.Paused = true;
        return CommandResult.Success(_world.Win.Message);
    }

    private CommandResult Absorb(CorpId target)
    {
        var owned = _world.Holdings.Where(h => h.Owner.Equals(_world.Player.Id) && h.Issuer.Equals(target)).Sum(h => h.Shares);
        var corp = _world.FindCorp(target);
        if (corp is null) return CommandResult.Fail("Target not found");
        if (owned < corp.SharesOutstanding * 0.5m)
            return CommandResult.Fail("Need ≥50% ownership");
        foreach (var firm in _world.Firms.Where(f => f.Owner.Equals(target)))
            firm.Owner = _world.Player.Id;
        _world.Player.Cash += corp.Cash;
        corp.Cash = 0;
        corp.Retired = true;
        _world.AddNews($"Absorbed {corp.Name}");
        return CommandResult.Success($"Absorbed {corp.Name}");
    }

    private Firm? RequirePlayerFirm(FirmId id, out CommandResult? err)
    {
        var firm = _world.FindFirm(id);
        if (firm is null) { err = CommandResult.Fail("Firm not found"); return null; }
        if (!firm.Owner.Equals(_world.Player.Id)) { err = CommandResult.Fail("Not your firm"); return null; }
        err = null;
        return firm;
    }

    private FunctionalUnit? RequireUnit(FirmId firmId, UnitId unitId, out CommandResult? err)
    {
        var firm = RequirePlayerFirm(firmId, out err);
        if (firm is null) return null;
        var unit = firm.Units.FirstOrDefault(u => u.Id.Equals(unitId));
        if (unit is null) { err = CommandResult.Fail("Unit not found"); return null; }
        err = null;
        return unit;
    }
}
