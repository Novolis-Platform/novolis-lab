using System.Globalization;
using Novolis.Economy.Core;
using Novolis.Economy.Core.Holdings;
using Novolis.Geopolitics.Core;

namespace PolityTriad;

static class MonthSampleFactory
{
    public static NationMonthFacts FromEconomyPolity(
        Polity polity,
        EconomyState eco,
        LegalEntityId state,
        LegalEntityId firm,
        RegionId region,
        decimal widgetsDelta,
        double productionValue,
        double forceDemand,
        double shortage,
        double control,
        double population,
        double emigrationPressure,
        double netMigration)
    {
        return new NationMonthFacts
        {
            StateCash = (double)eco.Entities[state].Cash.Amount,
            TaxCollected = (double)eco.Flows.TaxCollected.Amount,
            Transfers = (double)eco.Flows.TransfersPaid.Amount,
            Wages = (double)eco.Flows.WagesAccrued.Amount,
            WidgetsProduced = (double)widgetsDelta,
            ProductionValue = productionValue,
            OreStock = (double)HoldingLedger.GetQuantity(eco, firm, region, TriadWorld.OreId),
            CivicLastTax = polity.Civic.LastTaxCollected,
            CivicLastXfer = polity.Civic.LastTransfersPaid,
            Legitimacy = polity.Civic.Legitimacy,
            Approval = polity.Civic.Approval,
            WarFatigue = polity.Civic.WarFatigue,
            HumanDevelopment = polity.Civic.HumanDevelopment,
            Gdp = polity.Gdp,
            ForceDemand = forceDemand,
            ForceTotal = polity.Military.Total,
            GeoShortage = shortage,
            ControlRatio = control,
            Tech = polity.TechLevel,
            HouseholdTaxRate = polity.Policy.HouseholdTaxRate,
            MilitaryShare = polity.Policy.MilitaryShare,
            Population = population,
            EmigrationPressure = emigrationPressure,
            NetMigration = netMigration,
        };
    }

    public static NationMonthFacts FromGeoOnly(
        Polity polity, double shortage, double control,
        double population, double emigrationPressure, double netMigration) => new()
    {
        StateCash = polity.Treasury,
        TaxCollected = polity.Civic.LastTaxCollected,
        Transfers = polity.Civic.LastTransfersPaid,
        Legitimacy = polity.Civic.Legitimacy,
        Approval = polity.Civic.Approval,
        WarFatigue = polity.Civic.WarFatigue,
        HumanDevelopment = polity.Civic.HumanDevelopment,
        Gdp = polity.Gdp,
        ForceTotal = polity.Military.Total,
        GeoShortage = shortage,
        ControlRatio = control,
        Tech = polity.TechLevel,
        HouseholdTaxRate = polity.Policy.HouseholdTaxRate,
        MilitaryShare = polity.Policy.MilitaryShare,
        Population = population,
        EmigrationPressure = emigrationPressure,
        NetMigration = netMigration,
    };
}
