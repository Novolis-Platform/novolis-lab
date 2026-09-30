namespace CapitalistSimulator.Sim;

internal sealed class MonthTick
{
    private readonly GameWorld _world;

    public MonthTick(GameWorld world) => _world = world;

    public void RunMonth()
    {
        foreach (var corp in _world.Corporations)
        {
            corp.MonthRevenue = 0;
            corp.MonthExpense = 0;
        }

        _world.LastMonthSales.Clear();
        CompetitorAgent.Act(_world);
        RunExtract();
        RunPurchasing();
        RunManufacture();
        RunAdvertising();
        RunRd();
        RunRetailAndSales();
        RunExpensesAndLoans();
        RunHqAutomation();
        UpdateStockPrices();
        DriftEconomy();
        RecordPnl();
        WinConditions.Evaluate(_world);
    }

    private void RunExtract()
    {
        foreach (var firm in _world.Firms.Where(f => f.Kind is FirmKind.Farm or FirmKind.Extract))
        {
            var corp = _world.FindCorp(firm.Owner);
            if (corp is null || corp.Retired) continue;
            foreach (var unit in firm.Units.Where(u => u.Kind == UnitKind.Extract && u.ExtractProductId is not null))
            {
                var levelMul = 0.7 + unit.Level * 0.1 + unit.Training * 0.3;
                var noise = 0.85 + _world.Rng.NextDouble() * 0.3;
                var qty = (decimal)((double)unit.ExtractYield * levelMul * noise);
                if (qty <= 0) continue;
                var quality = Math.Clamp(0.45 + unit.Level * 0.05 + unit.Training * 0.2, 0.1, 0.95);
                var lot = _world.GetOrCreateLot(firm, unit.ExtractProductId!, quality);
                var oldQty = lot.Quantity;
                lot.Quality = oldQty <= 0 ? quality : (lot.Quality * (double)oldQty + quality * (double)qty) / (double)(oldQty + qty);
                lot.Quantity += qty;
                lot.UnitCost = _world.Catalog.Products.TryGetValue(unit.ExtractProductId!, out var p) ? p.BasePrice * 0.4m : 1;
            }
        }
    }

    private void RunPurchasing()
    {
        foreach (var firm in _world.Firms)
        {
            var corp = _world.FindCorp(firm.Owner);
            if (corp is null || corp.Retired) continue;
            foreach (var unit in firm.Units.Where(u => u.Kind == UnitKind.Purchasing && u.PurchaseProductId is not null))
            {
                var need = unit.PurchaseQtyTarget;
                if (need <= 0) continue;
                var productId = unit.PurchaseProductId!;
                // Replenish only what sold (plus small buffer), never refill past ~1.2 months of sell-through.
                var linkedSold = firm.Units
                    .Where(u => u.Kind == UnitKind.Sales
                        && string.Equals(u.SalesProductId, productId, StringComparison.OrdinalIgnoreCase))
                    .Sum(u => u.LastSold);
                var onHand = _world.FindLot(firm, productId)?.Quantity ?? 0;
                var desiredShelf = linkedSold > 0 ? linkedSold * 1.2m + 20m : (firm.Kind == FirmKind.Retail ? 220m : need);
                need = Math.Min(need, Math.Max(0m, desiredShelf - onHand));
                if (need <= 0) continue;

                if (!unit.PurchaseFromSeaport && unit.PurchaseFromFirm is { } fromId)
                {
                    BuyFromFirm(firm, corp, unit, fromId, productId, need);
                    continue;
                }

                if (corp.Hq.ImportPreferInternal)
                {
                    var sister = _world.FirmsOf(corp.Id)
                        .Where(f => !f.Id.Equals(firm.Id))
                        .SelectMany(f => f.Inventory.Select(lot => (f, lot)))
                        .Where(x => string.Equals(x.lot.ProductId, productId, StringComparison.OrdinalIgnoreCase) && x.lot.Quantity > 0)
                        .OrderByDescending(x => x.lot.Quantity)
                        .FirstOrDefault();
                    if (sister.f is not null)
                    {
                        var take = Math.Min(need, sister.lot.Quantity);
                        TransferInternal(sister.f, firm, productId, take, sister.lot.Quality, sister.lot.UnitCost, unit.PrivateLabel);
                        need -= take;
                    }
                }

                if (need > 0 && unit.PurchaseFromSeaport)
                    BuySeaport(firm, corp, productId, need, unit.PrivateLabel);
            }
        }
    }

    private void BuySeaport(Firm firm, Corporation corp, string productId, decimal need, bool privateLabel)
    {
        var offer = _world.Catalog.Seaport.FirstOrDefault(s =>
            string.Equals(s.ProductId, productId, StringComparison.OrdinalIgnoreCase));
        if (offer is null) return;
        var qty = Math.Min(need, offer.MonthlySupply);
        var cost = qty * offer.UnitCost;
        if (corp.Cash < cost)
        {
            qty = Math.Floor(corp.Cash / Math.Max(0.01m, offer.UnitCost));
            cost = qty * offer.UnitCost;
        }
        if (qty <= 0) return;
        corp.Cash -= cost; // inventory purchase — not P&L expense (COGS booked on sale)
        var quality = offer.Quality;
        if (privateLabel)
            quality = Math.Min(0.95, quality + 0.05);
        var lot = _world.GetOrCreateLot(firm, productId, quality);
        BlendLot(lot, qty, quality, offer.UnitCost);
    }

    private void BuyFromFirm(Firm buyer, Corporation buyerCorp, FunctionalUnit unit, FirmId fromId, string productId, decimal need)
    {
        var seller = _world.FindFirm(fromId);
        if (seller is null) return;
        var sellerCorp = _world.FindCorp(seller.Owner);
        if (sellerCorp is null) return;
        var lot = _world.FindLot(seller, productId);
        if (lot is null || lot.Quantity <= 0) return;
        var qty = Math.Min(need, lot.Quantity);
        var price = lot.UnitCost * 1.15m;
        var salesUnit = seller.Units.FirstOrDefault(u =>
            u.Kind == UnitKind.Sales && string.Equals(u.SalesProductId, productId, StringComparison.OrdinalIgnoreCase));
        if (salesUnit is not null && salesUnit.SalesPrice > 0)
            price = salesUnit.SalesPrice;
        var cost = qty * price;
        if (buyerCorp.Cash < cost)
        {
            qty = Math.Floor(buyerCorp.Cash / Math.Max(0.01m, price));
            cost = qty * price;
        }
        if (qty <= 0) return;
        var sellerCogs = qty * lot.UnitCost;
        lot.Quantity -= qty;
        buyerCorp.Cash -= cost; // inventory — COGS on buyer's later sale
        sellerCorp.Cash += cost;
        sellerCorp.MonthRevenue += cost;
        sellerCorp.MonthExpense += sellerCogs;
        var q = unit.PrivateLabel ? Math.Min(0.95, lot.Quality + 0.05) : lot.Quality;
        var dest = _world.GetOrCreateLot(buyer, productId, q);
        BlendLot(dest, qty, q, price);
        _world.LastMonthSales.Add(new MarketShareSnap
        {
            ProductId = productId,
            CorpId = sellerCorp.Id,
            UnitsSold = qty,
            Revenue = cost,
        });
    }

    private void TransferInternal(Firm from, Firm to, string productId, decimal qty, double quality, decimal unitCost, bool privateLabel)
    {
        var src = _world.FindLot(from, productId);
        if (src is null) return;
        qty = Math.Min(qty, src.Quantity);
        src.Quantity -= qty;
        var q = privateLabel ? Math.Min(0.95, quality + 0.05) : quality;
        var dest = _world.GetOrCreateLot(to, productId, q);
        BlendLot(dest, qty, q, unitCost);
    }

    private static void BlendLot(StockLot lot, decimal qty, double quality, decimal unitCost)
    {
        var old = lot.Quantity;
        if (old <= 0)
        {
            lot.Quantity = qty;
            lot.Quality = quality;
            lot.UnitCost = unitCost;
            return;
        }
        lot.Quality = (lot.Quality * (double)old + quality * (double)qty) / (double)(old + qty);
        lot.UnitCost = (lot.UnitCost * old + unitCost * qty) / (old + qty);
        lot.Quantity = old + qty;
    }

    private void RunManufacture()
    {
        foreach (var firm in _world.Firms.Where(f => f.Kind == FirmKind.Factory))
        {
            var corp = _world.FindCorp(firm.Owner);
            if (corp is null || corp.Retired) continue;
            foreach (var unit in firm.Units.Where(u => u.Kind == UnitKind.Manufacturing && u.RecipeOutputId is not null))
            {
                if (!_world.Catalog.RecipesByOutput.TryGetValue(unit.RecipeOutputId!, out var recipe))
                    continue;
                var levelMul = (0.6 + unit.Level * 0.15 + unit.Training * 0.4) * firm.FactorySize;
                var batches = (decimal)((double)unit.ProductionRate * levelMul);
                if (batches <= 0) continue;

                var maxBatches = batches;
                foreach (var input in recipe.Inputs)
                {
                    if (input.Qty <= 0) continue;
                    var lot = _world.FindLot(firm, input.Id);
                    var available = lot?.Quantity ?? 0;
                    maxBatches = Math.Min(maxBatches, available / input.Qty);
                }
                maxBatches = Math.Floor(maxBatches);
                if (maxBatches <= 0) continue;

                double inputQuality = 0.5;
                var qualitySamples = 0;
                decimal inputCost = 0;
                foreach (var input in recipe.Inputs)
                {
                    if (input.Qty <= 0) continue;
                    var lot = _world.FindLot(firm, input.Id)!;
                    var take = input.Qty * maxBatches;
                    inputQuality += lot.Quality;
                    qualitySamples++;
                    inputCost += take * lot.UnitCost;
                    lot.Quantity -= take;
                }

                var avgInQ = qualitySamples > 0 ? inputQuality / qualitySamples : 0.5;
                var tech = corp.Tech.ProductTech.GetValueOrDefault(unit.RecipeOutputId!, 0.5);
                var outQ = Math.Clamp(avgInQ * 0.6 + tech * 0.4 + unit.Training * 0.1, 0.05, 0.99);
                var outLot = _world.GetOrCreateLot(firm, unit.RecipeOutputId!, outQ);
                var unitCost = inputCost / maxBatches;
                BlendLot(outLot, maxBatches, outQ, unitCost);
            }
        }
    }

    private void RunAdvertising()
    {
        foreach (var firm in _world.Firms)
        {
            var corp = _world.FindCorp(firm.Owner);
            if (corp is null || corp.Retired) continue;
            foreach (var unit in firm.Units.Where(u => u.Kind == UnitKind.Advertising && u.AdBudget > 0))
            {
                var spend = Math.Min(unit.AdBudget, corp.Cash);
                if (spend <= 0) continue;
                corp.Cash -= spend;
                corp.MonthExpense += spend;
                var key = ResolveBrandKey(corp, unit.AdProductId, unit.AdClass);
                if (!_world.Catalog.Products.ContainsKey(key) && unit.AdProductId is null && unit.AdClass is null)
                    key = "corporate";
                if (!corp.Brands.TryGetValue(key, out var brand))
                {
                    brand = new BrandState();
                    corp.Brands[key] = brand;
                }
                var lift = Math.Sqrt((double)spend) / 500.0;
                brand.Awareness = Math.Clamp(brand.Awareness + lift, 0, 1);
                brand.Loyalty = Math.Clamp(brand.Loyalty + lift * 0.4, 0, 1);
            }
        }
    }

    private static string ResolveBrandKey(Corporation corp, string? productId, string? productClass) =>
        corp.BrandStrategy switch
        {
            BrandStrategy.Corporate => "corporate",
            BrandStrategy.Range => productClass ?? "corporate",
            BrandStrategy.Unique => productId ?? "corporate",
            _ => "corporate",
        };

    private void RunRd()
    {
        foreach (var firm in _world.Firms.Where(f => f.Kind == FirmKind.Rd))
        {
            var corp = _world.FindCorp(firm.Owner);
            if (corp is null || corp.Retired) continue;
            var active = firm.Units.Where(u => u.Kind == UnitKind.Rd && u.RdTargetProductId is not null).ToList();
            var combo = Math.Min(active.Count, 9);
            var comboBonus = 1.0 + combo * 0.08;
            foreach (var unit in active)
            {
                unit.RdMonthsRemaining--;
                unit.RdProgress += (0.15 + unit.Training * 0.1) * comboBonus;
                var cost = 8000m * (decimal)(1 + unit.Level * 0.2);
                if (corp.Cash >= cost)
                {
                    corp.Cash -= cost;
                    corp.MonthExpense += cost;
                }
                if (unit.RdMonthsRemaining <= 0 || unit.RdProgress >= 1)
                {
                    var pid = unit.RdTargetProductId!;
                    if (firm.AutoApplyRd)
                    {
                        var prev = corp.Tech.ProductTech.GetValueOrDefault(pid, 0.5);
                        corp.Tech.ProductTech[pid] = Math.Clamp(prev + 0.08 * comboBonus, 0.1, 0.99);
                        _world.AddNews($"{corp.Name} improved tech for {pid}");
                    }
                    unit.RdTargetProductId = null;
                    unit.RdMonthsRemaining = 0;
                    unit.RdProgress = 0;
                }
            }
        }
    }

    private void RunRetailAndSales()
    {
        foreach (var city in _world.Cities)
        {
            var climateMul = city.Climate switch
            {
                EconomicClimate.Boom => 1.25,
                EconomicClimate.Growth => 1.1,
                EconomicClimate.Stable => 1.0,
                EconomicClimate.Slowdown => 0.85,
                EconomicClimate.Recession => 0.7,
                EconomicClimate.Panic => 0.5,
                _ => 1.0,
            };
            // Cap2-scale grocery throughput across SKUs.
            var demandBase = (double)city.Population / 8.0 * city.SpendingLevel * climateMul;

            var retailFirms = _world.Firms.Where(f => f.CityId.Equals(city.Id) && f.Kind == FirmKind.Retail).ToList();
            var byProduct = new Dictionary<string, List<(Firm Firm, FunctionalUnit Unit, Corporation Corp, StockLot Lot)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var firm in retailFirms)
            {
                var corp = _world.FindCorp(firm.Owner);
                if (corp is null || corp.Retired) continue;
                foreach (var unit in firm.Units.Where(u => u.Kind == UnitKind.Sales && u.SalesProductId is not null))
                {
                    var lot = _world.FindLot(firm, unit.SalesProductId!);
                    if (lot is null || lot.Quantity <= 0) continue;
                    if (unit.SalesPrice <= 0) continue;
                    if (!byProduct.TryGetValue(unit.SalesProductId!, out var list))
                    {
                        list = [];
                        byProduct[unit.SalesProductId!] = list;
                    }
                    list.Add((firm, unit, corp, lot));
                }
            }

            foreach (var (productId, offers) in byProduct)
            {
                if (!_world.Catalog.Products.TryGetValue(productId, out var prod)) continue;
                var cityDemand = (decimal)(demandBase * prod.Necessity * (0.8 + _world.Rng.NextDouble() * 0.4));
                var scored = offers.Select(o =>
                {
                    var brandKey = ResolveBrandKey(o.Corp, productId, prod.Class.ToString());
                    o.Corp.Brands.TryGetValue(brandKey, out var brand);
                    brand ??= o.Corp.Brands.GetValueOrDefault("corporate") ?? new BrandState();
                    var priceAttr = AttractivenessPrice(o.Unit.SalesPrice, prod.BasePrice);
                    var q = o.Lot.Quality;
                    var b = brand.Awareness * 0.6 + brand.Loyalty * 0.4;
                    var score = priceAttr * 0.45 + q * 0.35 + b * 0.20;
                    return (o, score);
                }).OrderByDescending(x => x.score).ToList();

                var totalScore = scored.Sum(x => x.score);
                if (totalScore <= 0) continue;
                var remaining = cityDemand;
                foreach (var (o, score) in scored)
                {
                    var share = (decimal)(score / totalScore);
                    var want = remaining * share * (decimal)(0.7 + score);
                    var sold = Math.Min(want, o.Lot.Quantity);
                    o.Unit.LastUnmetDemand = Math.Max(0, want - sold);
                    if (sold <= 0)
                    {
                        o.Unit.LastSold = 0;
                        continue;
                    }
                    var cogs = sold * o.Lot.UnitCost;
                    o.Lot.Quantity -= sold;
                    var revenue = sold * o.Unit.SalesPrice;
                    o.Corp.Cash += revenue;
                    o.Corp.MonthRevenue += revenue;
                    o.Corp.MonthExpense += cogs;
                    o.Unit.LastSold = sold;
                    // satisfaction → loyalty
                    var brandKey = ResolveBrandKey(o.Corp, productId, prod.Class.ToString());
                    if (!o.Corp.Brands.TryGetValue(brandKey, out var brand))
                    {
                        brand = new BrandState();
                        o.Corp.Brands[brandKey] = brand;
                    }
                    brand.Loyalty = Math.Clamp(brand.Loyalty + (score - 0.4) * 0.02, 0, 1);
                    _world.LastMonthSales.Add(new MarketShareSnap
                    {
                        ProductId = productId,
                        CorpId = o.Corp.Id,
                        UnitsSold = sold,
                        Revenue = revenue,
                    });
                    remaining -= sold;
                }
            }
        }

        // Wholesale dump for factory/farm/extract sales units
        foreach (var firm in _world.Firms.Where(f => f.Kind is FirmKind.Factory or FirmKind.Farm or FirmKind.Extract))
        {
            var corp = _world.FindCorp(firm.Owner);
            if (corp is null) continue;
            foreach (var unit in firm.Units.Where(u => u.Kind == UnitKind.Sales && u.SalesProductId is not null && u.SalesPrice > 0))
            {
                var lot = _world.FindLot(firm, unit.SalesProductId!);
                if (lot is null || lot.Quantity <= 0) continue;
                var sold = Math.Min(lot.Quantity, Math.Max(5m, lot.Quantity * 0.35m));
                var cogs = sold * lot.UnitCost;
                lot.Quantity -= sold;
                var revenue = sold * unit.SalesPrice;
                corp.Cash += revenue;
                corp.MonthRevenue += revenue;
                corp.MonthExpense += cogs;
                unit.LastSold = sold;
                _world.LastMonthSales.Add(new MarketShareSnap
                {
                    ProductId = unit.SalesProductId!,
                    CorpId = corp.Id,
                    UnitsSold = sold,
                    Revenue = revenue,
                });
            }
        }
    }

    private static double AttractivenessPrice(decimal price, decimal basePrice)
    {
        if (basePrice <= 0) return 0.5;
        var ratio = (double)(price / basePrice);
        // At 1.0× base → ~1.0; at 1.5× still ~0.75; only extreme gouging collapses demand.
        return Math.Clamp(1.25 - (ratio - 1.0) * 0.5, 0.2, 1.25);
    }

    private void RunExpensesAndLoans()
    {
        foreach (var firm in _world.Firms)
        {
            var corp = _world.FindCorp(firm.Owner);
            if (corp is null || corp.Retired) continue;
            // Inventory tiles are layout only — don't staff them. Keep wages lean vs grocery margin.
            var wages = firm.Units
                .Where(u => u.Kind != UnitKind.Inventory)
                .Sum(u => 55m + u.Level * 20m + (decimal)u.Training * 25m);
            var expense = firm.MonthlyExpense + wages;
            corp.Cash -= expense;
            corp.MonthExpense += expense;
            firm.LastMonthProfit = 0; // filled after corp rollup
            foreach (var u in firm.Units)
            {
                if (_world.Rng.NextDouble() < u.Training * 0.05 && u.Level < 9)
                    u.Level++;
            }
        }

        foreach (var corp in _world.Corporations.Where(c => !c.Retired))
        {
            foreach (var loan in corp.Loans.ToList())
            {
                var interest = loan.Principal * loan.MonthlyRate;
                corp.Cash -= interest;
                corp.MonthExpense += interest;
                var principalPay = Math.Min(loan.Principal * 0.02m, Math.Max(0, corp.Cash));
                loan.Principal -= principalPay;
                corp.Cash -= principalPay;
                if (loan.Principal <= 1)
                    corp.Loans.Remove(loan);
            }

            if (corp.DividendPerShare > 0 && corp.SharesOutstanding > 0)
            {
                var totalDiv = corp.DividendPerShare * corp.SharesOutstanding;
                if (corp.Cash >= totalDiv)
                {
                    corp.Cash -= totalDiv;
                    foreach (var h in _world.Holdings.Where(h => h.Issuer.Equals(corp.Id)))
                    {
                        var owner = _world.FindCorp(h.Owner);
                        if (owner is not null)
                            owner.Cash += h.Shares * corp.DividendPerShare;
                    }
                }
            }
        }
    }

    private void RunHqAutomation()
    {
        var player = _world.Player;
        if (player.Hq.MarketingAutoAds)
        {
            foreach (var firm in _world.FirmsOf(player.Id).Where(f => f.Kind == FirmKind.Retail))
            {
                foreach (var ad in firm.Units.Where(u => u.Kind == UnitKind.Advertising))
                {
                    if (ad.AdBudget <= 0)
                        ad.AdBudget = 2000;
                    var sales = firm.Units.FirstOrDefault(u => u.Kind == UnitKind.Sales && u.SalesProductId is not null);
                    if (sales is not null)
                        ad.AdProductId ??= sales.SalesProductId;
                }
            }
        }
        if (player.Hq.RdAutoStart)
        {
            foreach (var firm in _world.FirmsOf(player.Id).Where(f => f.Kind == FirmKind.Rd))
            {
                foreach (var rd in firm.Units.Where(u => u.Kind == UnitKind.Rd && u.RdTargetProductId is null))
                {
                    var target = _world.LastMonthSales
                        .Where(s => s.CorpId.Equals(player.Id))
                        .OrderByDescending(s => s.Revenue)
                        .Select(s => s.ProductId)
                        .FirstOrDefault() ?? "wine";
                    rd.RdTargetProductId = target;
                    rd.RdMonthsRemaining = 6;
                }
            }
        }
        if (player.Hq.FinanceAutoDividend && player.Cash > player.OpeningCash * 1.5m)
            player.DividendPerShare = Math.Max(player.DividendPerShare, 0.05m);
    }

    private void UpdateStockPrices()
    {
        foreach (var corp in _world.Corporations.Where(c => !c.Retired))
        {
            var profit = corp.MonthRevenue - corp.MonthExpense;
            corp.LastYearProfit = corp.LastYearProfit * 0.92m + profit * 0.08m;
            var book = Math.Max(1m, corp.Cash + _world.FirmsOf(corp.Id).Sum(f => f.Inventory.Sum(l => l.Quantity * l.UnitCost)));
            var target = book / Math.Max(1m, corp.SharesOutstanding) * 10m + corp.LastYearProfit / Math.Max(1m, corp.SharesOutstanding) * 40m;
            corp.SharePrice = Math.Max(0.5m, corp.SharePrice * 0.85m + target * 0.15m);
        }
    }

    private void DriftEconomy()
    {
        foreach (var city in _world.Cities)
        {
            city.SpendingLevel = Math.Clamp(city.SpendingLevel + (_world.Rng.NextDouble() - 0.5) * 0.03, 0.7, 1.4);
            // Weighted climates — Panic/Recession are rare so retail chains stay solvent long enough to win.
            if (_world.Rng.NextDouble() < 0.06)
            {
                var roll = _world.Rng.NextDouble();
                city.Climate = roll switch
                {
                    < 0.28 => EconomicClimate.Boom,
                    < 0.55 => EconomicClimate.Growth,
                    < 0.82 => EconomicClimate.Stable,
                    < 0.93 => EconomicClimate.Slowdown,
                    < 0.98 => EconomicClimate.Recession,
                    _ => EconomicClimate.Panic,
                };
                _world.AddNews($"{city.Name} climate → {city.Climate}");
            }
        }
    }

    private void RecordPnl()
    {
        foreach (var corp in _world.Corporations)
        {
            var profit = corp.MonthRevenue - corp.MonthExpense;
            corp.MonthlyPnl[corp.PnlCursor % 12] = profit;
            corp.PnlCursor++;
            corp.MonthsRecorded = Math.Min(12, corp.MonthsRecorded + 1);
            corp.TrailingYearProfit = 0;
            var slots = corp.MonthsRecorded >= 12 ? 12 : corp.MonthsRecorded;
            for (var i = 0; i < slots; i++)
                corp.TrailingYearProfit += corp.MonthlyPnl[i];
            // Keep EMA for stock pricing; win conditions use TrailingYearProfit.
            foreach (var firm in _world.FirmsOf(corp.Id))
                firm.LastMonthProfit = profit / Math.Max(1, _world.FirmsOf(corp.Id).Count());
        }
    }
}
