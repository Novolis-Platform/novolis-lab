using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation;

internal static class CalypsoBridgeChecks
{
    public static bool PilotsForwardOfCaptain(CadDocument doc, float loa)
    {
        var captain = doc.Entities.FirstOrDefault(e =>
            string.Equals(e.Name, "outfit-BRIDGE-captain-chair", StringComparison.OrdinalIgnoreCase));
        var pilotP = doc.Entities.FirstOrDefault(e =>
            string.Equals(e.Name, "outfit-BRIDGE-pilot-port", StringComparison.OrdinalIgnoreCase));
        var pilotS = doc.Entities.FirstOrDefault(e =>
            string.Equals(e.Name, "outfit-BRIDGE-pilot-stbd", StringComparison.OrdinalIgnoreCase));
        if (captain?.Points is not { Count: >= 1 } || pilotP?.Points is not { Count: >= 1 })
            return false;
        var captainStem = loa * 0.5f - captain.Points[0][2];
        var pilotStemP = loa * 0.5f - pilotP.Points[0][2];
        var pilotStemS = pilotS?.Points is { Count: >= 1 } p ? loa * 0.5f - p[0][2] : pilotStemP;
        return pilotStemP < captainStem - 0.35f && pilotStemS < captainStem - 0.35f;
    }
}
