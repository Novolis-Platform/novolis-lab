using System.Globalization;
using Novolis.Economy.Core;
using Novolis.Economy.Core.Holdings;
using Novolis.Geopolitics.Core;

namespace PolityTriad;

sealed class MonthSample
{
    public required int Month { get; init; }
    public required string Phase { get; init; }
    public required bool AtWar { get; init; }
    public required int Battles { get; init; }
    public required double TradeDelta { get; init; }
    public required NationMonthFacts Alpha { get; init; }
    public required NationMonthFacts Beta { get; init; }
    public required NationMonthFacts Gamma { get; init; }
}
