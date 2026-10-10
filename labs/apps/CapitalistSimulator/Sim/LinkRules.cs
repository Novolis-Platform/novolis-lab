namespace CapitalistSimulator.Sim;

internal static class LinkRules
{
    public static bool CanLink(UnitKind from, UnitKind to) => (from, to) switch
    {
        (UnitKind.Purchasing, UnitKind.Manufacturing) => true,
        (UnitKind.Purchasing, UnitKind.Sales) => true,
        (UnitKind.Purchasing, UnitKind.Inventory) => true,
        (UnitKind.Extract, UnitKind.Manufacturing) => true,
        (UnitKind.Extract, UnitKind.Sales) => true,
        (UnitKind.Extract, UnitKind.Inventory) => true,
        (UnitKind.Manufacturing, UnitKind.Sales) => true,
        (UnitKind.Manufacturing, UnitKind.Inventory) => true,
        (UnitKind.Inventory, UnitKind.Sales) => true,
        (UnitKind.Inventory, UnitKind.Manufacturing) => true,
        (UnitKind.Advertising, UnitKind.Sales) => true,
        _ => false,
    };

    public static void AutoLink(Firm firm)
    {
        firm.Links.Clear();
        var purchasing = firm.Units.Where(u => u.Kind == UnitKind.Purchasing).ToList();
        var extract = firm.Units.Where(u => u.Kind == UnitKind.Extract).ToList();
        var mfg = firm.Units.Where(u => u.Kind == UnitKind.Manufacturing).ToList();
        var inv = firm.Units.Where(u => u.Kind == UnitKind.Inventory).ToList();
        var sales = firm.Units.Where(u => u.Kind == UnitKind.Sales).ToList();
        var ads = firm.Units.Where(u => u.Kind == UnitKind.Advertising).ToList();

        void Link(FunctionalUnit? a, FunctionalUnit? b)
        {
            if (a is null || b is null) return;
            if (!CanLink(a.Kind, b.Kind)) return;
            if (firm.Links.Any(l => l.From.Equals(a.Id) && l.To.Equals(b.Id))) return;
            firm.Links.Add((a.Id, b.Id));
        }

        foreach (var p in purchasing)
        {
            Link(p, mfg.FirstOrDefault());
            Link(p, inv.FirstOrDefault());
            Link(p, sales.FirstOrDefault());
        }
        foreach (var e in extract)
        {
            Link(e, mfg.FirstOrDefault());
            Link(e, inv.FirstOrDefault());
            Link(e, sales.FirstOrDefault());
        }
        foreach (var m in mfg)
        {
            Link(m, inv.FirstOrDefault());
            Link(m, sales.FirstOrDefault());
        }
        foreach (var i in inv)
            Link(i, sales.FirstOrDefault());
        foreach (var a in ads)
            Link(a, sales.FirstOrDefault());
    }
}
