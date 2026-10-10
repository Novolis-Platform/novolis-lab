using Novolis.Geopolitics.Core;

namespace GeoPolity.Session;

public static class MilitaryBuildCosts
{
    public static double UnitCost(MilitaryDomain domain) => domain switch
    {
        MilitaryDomain.Land => 50,
        MilitaryDomain.Air => 80,
        MilitaryDomain.Naval => 100,
        _ => 60,
    };

    public static bool TryParse(string? value, out MilitaryDomain domain)
    {
        domain = MilitaryDomain.Land;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value, ignoreCase: true, out domain);
    }
}
