using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Core.Extensions;
using Novolis.Economy.Finance;
using Novolis.Economy.Logistics;
using Novolis.Economy.Markets;
using Novolis.Economy.Population;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;
using Spectre.Console;

namespace NearSolPolity;

internal static class DurationArg
{
  public static bool TryParse(string? text, out long hours)
  {
    hours = 0;
    if (string.IsNullOrWhiteSpace(text))
    {
      return false;
    }

    text = text.Trim();
    var suffix = text[^1];
    var numberPart = char.IsLetter(suffix) ? text[..^1] : text;
    if (!decimal.TryParse(numberPart, System.Globalization.NumberStyles.Number,
          System.Globalization.CultureInfo.InvariantCulture, out var value)
        || value <= 0m)
    {
      return false;
    }

    hours = suffix switch
    {
      'd' or 'D' => (long)Math.Ceiling(value * SimulationHour.HoursPerDay),
      'h' or 'H' => (long)Math.Ceiling(value),
      _ when !char.IsLetter(suffix) => (long)Math.Ceiling(value),
      _ => 0,
    };
    return hours > 0;
  }

  public static string Format(long hours) =>
    hours % SimulationHour.HoursPerDay == 0
      ? $"{hours / SimulationHour.HoursPerDay}d ({hours}h)"
      : $"{hours}h (~{hours / (double)SimulationHour.HoursPerDay:0.#}d)";
}
