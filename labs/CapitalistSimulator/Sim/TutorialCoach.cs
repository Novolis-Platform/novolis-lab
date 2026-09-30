namespace CapitalistSimulator.Sim;

internal sealed record CoachStep(string Title, string Body, string PrimaryAction);

internal static class TutorialCoach
{
    public static CoachStep Next(GameWorld world)
    {
        if (world.Win.Won)
            return new("You won", world.Win.Message, "Reports");
        if (world.Win.Lost)
            return new("Game over", world.Win.Message, "New");

        var playerFirms = world.FirmsOf(world.Player.Id).ToList();
        var retail = playerFirms.FirstOrDefault(f => f.Kind == FirmKind.Retail);

        if (retail is null)
            return new(
                "Open your first store",
                "Click an empty green tile on the map, leave Type on a supermarket, then press Build store.",
                "Build store");

        var salesReady = retail.Units.Count(u => u.Kind == UnitKind.Sales && u.SalesProductId is not null);
        if (salesReady == 0)
            return new(
                "Stock the shelves",
                "Select Corner Market → pick a sales slot and product (bread) → Apply price. Or press Fix starter stock.",
                "Fix starter stock");

        var sold = world.LastMonthSales.Where(s => s.CorpId.Equals(world.Player.Id)).Sum(s => s.Revenue);
        if (world.Day < 30 || sold <= 0)
            return new(
                "Run a month",
                "Your store is stocked from the seaport. Press Advance month to buy, sell, and see cash move. Watch the P/Q/B bars on the right.",
                "Advance month");

        var pnl = world.Player.MonthRevenue - world.Player.MonthExpense;
        if (pnl < 0)
            return new(
                "Tighten the store",
                "You're losing money. Raise shelf prices a little, or cut the Ad budget in Ops. Then Advance month again.",
                "Advance month");

        if (playerFirms.Count < 2)
            return new(
                "Expand",
                "You're profitable. Build a second store in this city (or switch City → Harbor Bay), stock it, and keep advancing months.",
                "Build store");

        if (!playerFirms.Any(f => f.Kind == FirmKind.Factory))
            return new(
                "Make your own goods",
                "Build a small factory, set a recipe in Ops (e.g. bread←flour), link purchasing→manufacturing→sales, then sell into your stores.",
                "Build store");

        return new(
            "Grow the empire",
            "Use Bank if cash is tight, Brand + ads for share, Stock when you're ready to IPO-style raise. Reports show what's selling.",
            "Advance month");
    }
}
