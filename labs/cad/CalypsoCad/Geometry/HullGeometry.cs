using CalypsoCad.Structure;

namespace CalypsoCad.Geometry;

/// <summary>
/// Immutable hull loft snapshot — authoritative section and ring queries for one generate pass.
/// Generators consume this; they do not call CalypsoLockGenerator section statics.
/// </summary>
internal sealed class HullGeometry
{
    private const float SectionChamfer = 2.5f;

    /// <summary>Active snapshot for the current generate / audit process.</summary>
    public static HullGeometry? Active { get; private set; }

    private readonly (float Z, float Beam, float H)[] _stations;
    private readonly float _oahMid;

    public float OahMid => _oahMid;
    public float Loa => _stations.Length == 0 ? 0f : _stations[^1].Z;
    public IReadOnlyList<(float Z, float Beam, float H)> Stations => _stations;

    public HullGeometry(IReadOnlyList<(float Z, float Beam, float H)> stations, float oahMid)
    {
        ArgumentNullException.ThrowIfNull(stations);
        if (stations.Count < 2)
            throw new ArgumentException("Hull loft needs ≥2 stations.", nameof(stations));
        _stations = stations.ToArray();
        _oahMid = oahMid;
    }

    public static void SetActive(HullGeometry geometry)
    {
        Active = geometry ?? throw new ArgumentNullException(nameof(geometry));
    }

    public static HullGeometry RequireActive() =>
        Active ?? throw new InvalidOperationException(
            "HullGeometry.Active is not set — call SetActive after loft load.");

    public float HullBeamAt(float zFromStem) => SampleLoft(zFromStem, s => s.Beam);

    public float HullHeightAt(float zFromStem) => SampleLoft(zFromStem, s => s.H);

    public void SectionAt(
        float z,
        out float halfB,
        out float halfH,
        out float mid,
        out float cx,
        out float cy)
    {
        var beam = MathF.Max(0.2f, HullBeamAt(z));
        var height = MathF.Max(0.2f, HullHeightAt(z));
        halfB = beam * 0.5f;
        halfH = height * 0.5f;
        mid = _oahMid;
        cx = MathF.Min(SectionChamfer * (beam / 20f), halfB * 0.45f);
        cy = MathF.Min(SectionChamfer * (height / 12f), halfH * 0.45f);
    }

    public bool HasStructuralSection(float zStem, float minHalf = 0.45f)
    {
        SectionAt(zStem, out var hb, out var hh, out _, out _, out _);
        return hb >= minHalf && hh >= minHalf;
    }

    public (float Y, float Up)[] OuterOctagonRing(float zStem)
    {
        SectionAt(zStem, out var halfB, out var halfH, out var mid, out var cx, out var cy);
        return
        [
            (-halfB + cx, mid + halfH),
            (halfB - cx, mid + halfH),
            (halfB, mid + halfH - cy),
            (halfB, mid - halfH + cy),
            (halfB - cx, mid - halfH),
            (-halfB + cx, mid - halfH),
            (-halfB, mid - halfH + cy),
            (-halfB, mid + halfH - cy),
        ];
    }

    public (float Y, float Up)[] InnerOctagonRing(float zStem, float skinInset)
    {
        var outer = OuterOctagonRing(zStem);
        if (skinInset <= 1e-6f)
            return outer;
        var inset = PolygonInset2D.Inset(outer, skinInset);
        return inset.Length >= 3 ? inset : outer;
    }

    public float HalfBeamInside(float z, float up, float inset)
    {
        SectionAt(z, out var halfB, out var halfH, out var mid, out var cx, out var cy);
        var local = up - mid;
        float half;
        if (local >= halfH - cy)
        {
            var t = Math.Clamp((local - (halfH - cy)) / MathF.Max(cy, 1e-4f), 0f, 1f);
            half = halfB - t * cx;
        }
        else if (local <= -halfH + cy)
        {
            var t = Math.Clamp(((-halfH + cy) - local) / MathF.Max(cy, 1e-4f), 0f, 1f);
            half = halfB - t * cx;
        }
        else
        {
            half = halfB;
        }

        return MathF.Max(0.05f, half - inset);
    }

    private float SampleLoft(float zFromStem, Func<(float Z, float Beam, float H), float> pick)
    {
        var stations = _stations;
        if (zFromStem <= stations[0].Z)
            return pick(stations[0]);
        if (zFromStem >= stations[^1].Z)
            return pick(stations[^1]);
        for (var i = 0; i < stations.Length - 1; i++)
        {
            var a = stations[i];
            var b = stations[i + 1];
            if (zFromStem > b.Z)
                continue;
            var t = (zFromStem - a.Z) / MathF.Max(1e-4f, b.Z - a.Z);
            return pick(a) + (pick(b) - pick(a)) * t;
        }

        return pick(stations[^1]);
    }
}
