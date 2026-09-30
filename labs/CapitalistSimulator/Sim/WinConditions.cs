namespace CapitalistSimulator.Sim;

internal static class WinConditions
{
    public static void Evaluate(GameWorld world)
    {
        if (world.Win.Won || world.Win.Lost) return;

        if (world.Player.Cash < -50_000)
        {
            world.Win.Lost = true;
            world.Win.Message = "Bankrupt — cash deeply negative.";
            world.Paused = true;
            return;
        }

        if (world.Day > world.ScenarioMaxDays)
        {
            world.Win.Lost = true;
            world.Win.Message = "Time expired.";
            world.Paused = true;
            return;
        }

        switch (world.Scenario)
        {
            case ScenarioId.Sandbox:
                break;
            case ScenarioId.RetailProfit:
            {
                var retailCount = world.FirmsOf(world.Player.Id).Count(f => f.Kind == FirmKind.Retail);
                var trailing = world.Player.TrailingYearProfit;
                if (trailing >= world.ScenarioTargetProfit && retailCount >= 2)
                    Win(world, $"Trailing-year profit ${trailing:N0} with retail chain.");
                break;
            }
            case ScenarioId.WineDominance:
            {
                var wineSales = world.LastMonthSales
                    .Where(s => string.Equals(s.ProductId, "wine", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var total = wineSales.Sum(s => s.UnitsSold);
                var mine = wineSales.Where(s => s.CorpId.Equals(world.Player.Id)).Sum(s => s.UnitsSold);
                var hasFarm = world.FirmsOf(world.Player.Id).Any(f => f.Kind == FirmKind.Farm);
                var hasFactory = world.FirmsOf(world.Player.Id).Any(f =>
                    f.Kind == FirmKind.Factory && f.Units.Any(u => u.RecipeOutputId == "wine"));
                if (hasFarm && hasFactory && total > 0 && mine / total >= 0.55m)
                    Win(world, "Dominating wine market with vertical integration.");
                break;
            }
        }
    }

    private static void Win(GameWorld world, string message)
    {
        world.Win.Won = true;
        world.Win.Message = message;
        world.Paused = true;
        world.AddNews($"Victory: {message}");
    }
}
