using Novolis.Geopolitics.Core;

namespace GeoPolity.Session;

/// <summary>Pure session command helpers — keys, Avalonia buttons, and Agent Execute share this path.</summary>
public static class GeoSessionCommands
{
    public static void Pause(GeoSession session) => session.Clock.Pause();

    public static void Resume(GeoSession session) => session.Clock.Resume();

    public static void ToggleRun(GeoSession session) => session.Clock.ToggleRun();

    public static void SetSpeed(GeoSession session, int preset) =>
        session.Clock.SetSpeedPreset(preset);

    public static void Step(GeoSession session, int days)
    {
        var d = Math.Clamp(days, 1, 3650);
        session.AdvanceDays(d);
        session.Clock.StatusNote = $"step {d}d";
    }

    public static void AdvanceYears(GeoSession session, int years)
    {
        var y = Math.Clamp(years, 1, 100);
        session.AdvanceYears(y);
        session.Clock.StatusNote = $"advanced {y}y";
    }

    public static void Quit(GeoSession session) => session.RequestQuit();

    public static void SelectSystem(GeoSession session, PolityId id) => session.SelectSystem(id);

    public static void SelectSystem(GeoSession session, int id) =>
        session.SelectSystem(new PolityId(id));

    /// <summary>Player-only military budget share [0, 0.7].</summary>
    public static void SetMilitaryShare(GeoSession session, double share)
    {
        var player = session.Player;
        var clamped = Math.Clamp(share, 0.05, 0.7);
        player.Policy.MilitaryShare = clamped;
        player.MilitaryBudgetShare = clamped;
        session.Clock.StatusNote = $"mil share {clamped:0%}";
    }

    /// <summary>Player-only instant force build; spends treasury.</summary>
    public static string OrderBuild(GeoSession session, MilitaryDomain domain, double amount)
    {
        var qty = Math.Clamp(amount, 1, 500);
        var cost = qty * MilitaryBuildCosts.UnitCost(domain);
        var player = session.Player;
        if (player.Treasury < cost)
        {
            session.Clock.StatusNote = "insufficient treasury";
            return session.Clock.StatusNote;
        }

        player.Treasury -= cost;
        switch (domain)
        {
            case MilitaryDomain.Land:
                player.Military.Land += qty;
                break;
            case MilitaryDomain.Air:
                player.Military.Air += qty;
                break;
            case MilitaryDomain.Naval:
                player.Military.Naval += qty;
                break;
        }

        var msg = $"{player.Name} builds {qty:0} {domain} (−{cost:0} treasury)";
        // Player lever outside CivicEngine monthly force accumulation.
        session.World.AddEvent(GeoEventKind.ForceExpansion, msg, player.Id);
        session.Headlines.SyncFrom(session.World);
        session.Clock.StatusNote = msg;
        return msg;
    }
}
