using System.Globalization;
using Novolis.Economy.Core;
using Novolis.Economy.Core.Holdings;
using Novolis.Geopolitics.Core;

namespace PolityTriad;

sealed class NationMonthFacts
{
    public double StateCash { get; init; }
    public double TaxCollected { get; init; }
    public double Transfers { get; init; }
    public double Wages { get; init; }
    public double WidgetsProduced { get; init; }
    public double ProductionValue { get; init; }
    public double OreStock { get; init; }
    public double CivicLastTax { get; init; }
    public double CivicLastXfer { get; init; }
    public double Legitimacy { get; init; }
    public double Approval { get; init; }
    public double WarFatigue { get; init; }
    public double HumanDevelopment { get; init; }
    public double Gdp { get; init; }
    public double ForceDemand { get; init; }
    public double ForceTotal { get; init; }
    public double GeoShortage { get; init; }
    public double ControlRatio { get; init; }
    public double Tech { get; init; }
    public double HouseholdTaxRate { get; init; }
    public double MilitaryShare { get; init; }
    public double Population { get; init; }
    public double EmigrationPressure { get; init; }
    public double NetMigration { get; init; }
}
