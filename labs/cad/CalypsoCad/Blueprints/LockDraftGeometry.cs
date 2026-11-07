using System.Globalization;
using System.Text.Json;

namespace CalypsoCad.Blueprints;

/// <summary>
/// Shared lock→draft geometry: hullLoft.stations[].verts + SharedPlanScale.
/// Internals and StructureDetail must both call this — no parallel HullBeamAt tables.
/// </summary>
internal static class LockDraftGeometry
{
    public const double PlanPadL = 22;
    public const double PlanPadR = 36;
    public const double PlanPadTop = 16;
    public const double PlanPadBot = 28;
    public const double PlanFit = 0.92;

    public sealed class LoftStation
    {
        public string? Id { get; init; }
        public double Z { get; init; }
        public double HalfBeam { get; init; }
        public double HalfHeight { get; init; }
        public List<(double Y, double Up)> Verts { get; init; } = [];
    }

    public static double SharedPlanScale(double loa, double beam, double planW, double planH) =>
        Math.Min((planW - PlanPadL - PlanPadR) / loa, (planH - PlanPadTop - PlanPadBot) / beam) * PlanFit;

    public static string ScaleNote(double scalePxPerM) =>
        $"{scalePxPerM.ToString("0.###", CultureInfo.InvariantCulture)} px/m";

    public static double ShipX(double ox, double scale, double loa, double zFromStem) =>
        ox + (loa - zFromStem) * scale;

    public static List<LoftStation> ParseLoft(JsonElement root)
    {
        var loft = new List<LoftStation>();
        if (!root.TryGetProperty("hullLoft", out var hl)
            || !hl.TryGetProperty("stations", out var sts)
            || sts.ValueKind != JsonValueKind.Array)
            return loft;

        foreach (var st in sts.EnumerateArray())
        {
            var verts = new List<(double Y, double Up)>();
            if (st.TryGetProperty("verts", out var vs) && vs.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in vs.EnumerateArray())
                    verts.Add((v.GetProperty("y").GetDouble(), v.GetProperty("up").GetDouble()));
            }

            loft.Add(new LoftStation
            {
                Id = st.TryGetProperty("id", out var id) ? id.GetString() : null,
                Z = st.GetProperty("zFromStem").GetDouble(),
                HalfBeam = st.GetProperty("halfBeam").GetDouble(),
                HalfHeight = st.GetProperty("halfHeight").GetDouble(),
                Verts = verts,
            });
        }

        loft.Sort((a, b) => a.Z.CompareTo(b.Z));
        return loft;
    }

    public static double LoftHalfBeam(IReadOnlyList<LoftStation> loft, double z) =>
        SampleLoftScalar(loft, z, s => s.HalfBeam);

    public static double LoftFullBeam(IReadOnlyList<LoftStation> loft, double z) =>
        LoftHalfBeam(loft, z) * 2;

    public static double LoftHeight(IReadOnlyList<LoftStation> loft, double z, double fallbackOah)
    {
        SampleLoftUpRange(loft, z, fallbackOah, out var upMin, out var upMax);
        return Math.Max(0.2, upMax - upMin);
    }

    public static double LoftWaterlineHalfBeam(IReadOnlyList<LoftStation> loft, double z, double up)
    {
        if (loft.Count == 0)
            return 0;
        if (z <= loft[0].Z)
            return WaterlineHalfBeamAtStation(loft[0], up);
        if (z >= loft[^1].Z)
            return WaterlineHalfBeamAtStation(loft[^1], up);
        for (var i = 0; i < loft.Count - 1; i++)
        {
            var a = loft[i];
            var b = loft[i + 1];
            if (z < a.Z || z > b.Z)
                continue;
            var t = (z - a.Z) / Math.Max(1e-9, b.Z - a.Z);
            return WaterlineHalfBeamAtStation(a, up) * (1 - t) + WaterlineHalfBeamAtStation(b, up) * t;
        }

        return WaterlineHalfBeamAtStation(loft[^1], up);
    }

    public static void SampleLoftUpRange(
        IReadOnlyList<LoftStation> loft, double z, double fallbackOah,
        out double upMin, out double upMax)
    {
        if (loft.Count == 0)
        {
            upMin = 0;
            upMax = fallbackOah;
            return;
        }

        LoftStation a;
        LoftStation b;
        double t;
        if (z <= loft[0].Z)
        {
            a = b = loft[0];
            t = 0;
        }
        else if (z >= loft[^1].Z)
        {
            a = b = loft[^1];
            t = 0;
        }
        else
        {
            a = loft[0];
            b = loft[^1];
            t = 0;
            for (var i = 0; i < loft.Count - 1; i++)
            {
                if (z < loft[i].Z || z > loft[i + 1].Z)
                    continue;
                a = loft[i];
                b = loft[i + 1];
                t = (z - a.Z) / Math.Max(1e-9, b.Z - a.Z);
                break;
            }
        }

        var aMin = a.Verts.Count == 0 ? 0 : a.Verts.Min(v => v.Up);
        var aMax = a.Verts.Count == 0 ? a.HalfHeight * 2 : a.Verts.Max(v => v.Up);
        var bMin = b.Verts.Count == 0 ? 0 : b.Verts.Min(v => v.Up);
        var bMax = b.Verts.Count == 0 ? b.HalfHeight * 2 : b.Verts.Max(v => v.Up);
        upMin = aMin + t * (bMin - aMin);
        upMax = aMax + t * (bMax - aMax);
    }

    public static List<(double Y, double Up)> StationVertsAt(IReadOnlyList<LoftStation> loft, double z)
    {
        if (loft.Count == 0)
            return [];
        LoftStation a;
        LoftStation b;
        double t;
        if (z <= loft[0].Z)
        {
            a = b = loft[0];
            t = 0;
        }
        else if (z >= loft[^1].Z)
        {
            a = b = loft[^1];
            t = 0;
        }
        else
        {
            a = loft[0];
            b = loft[^1];
            t = 0;
            for (var i = 0; i < loft.Count - 1; i++)
            {
                if (z < loft[i].Z || z > loft[i + 1].Z)
                    continue;
                a = loft[i];
                b = loft[i + 1];
                t = (z - a.Z) / Math.Max(1e-9, b.Z - a.Z);
                break;
            }
        }

        if (a.Verts.Count == 0)
            return [];
        if (a.Verts.Count != b.Verts.Count || ReferenceEquals(a, b) || t <= 0)
            return a.Verts.ToList();
        var outVerts = new List<(double Y, double Up)>(a.Verts.Count);
        for (var i = 0; i < a.Verts.Count; i++)
        {
            var (ay, au) = a.Verts[i];
            var (by, bu) = b.Verts[i];
            outVerts.Add((ay + t * (by - ay), au + t * (bu - au)));
        }

        return outVerts;
    }

    private static double SampleLoftScalar(
        IReadOnlyList<LoftStation> loft, double z, Func<LoftStation, double> pick)
    {
        if (loft.Count == 0)
            return 0;
        if (z <= loft[0].Z)
            return pick(loft[0]);
        if (z >= loft[^1].Z)
            return pick(loft[^1]);
        for (var i = 0; i < loft.Count - 1; i++)
        {
            var a = loft[i];
            var b = loft[i + 1];
            if (z < a.Z || z > b.Z)
                continue;
            var t = (z - a.Z) / Math.Max(1e-9, b.Z - a.Z);
            return pick(a) + t * (pick(b) - pick(a));
        }

        return pick(loft[^1]);
    }

    private static double WaterlineHalfBeamAtStation(LoftStation st, double up)
    {
        if (st.Verts.Count < 3)
            return st.HalfBeam;
        var maxAbs = 0.0;
        var hit = false;
        for (var i = 0; i < st.Verts.Count; i++)
        {
            var (y0, u0) = st.Verts[i];
            var (y1, u1) = st.Verts[(i + 1) % st.Verts.Count];
            if (Math.Abs(u1 - u0) < 1e-9)
            {
                if (Math.Abs(u0 - up) < 1e-4)
                {
                    maxAbs = Math.Max(maxAbs, Math.Abs(y0));
                    maxAbs = Math.Max(maxAbs, Math.Abs(y1));
                    hit = true;
                }

                continue;
            }

            if ((u0 - up) * (u1 - up) > 0)
                continue;
            var t = (up - u0) / (u1 - u0);
            if (t is < -0.02 or > 1.02)
                continue;
            t = Math.Clamp(t, 0, 1);
            maxAbs = Math.Max(maxAbs, Math.Abs(y0 + t * (y1 - y0)));
            hit = true;
        }

        return hit ? maxAbs : 0;
    }
}
