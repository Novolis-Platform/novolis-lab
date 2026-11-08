namespace CalypsoCad.Geometry;

/// <summary>
/// Continuous 3D reference path for one outer-hull plating knuckle (octagon vertex index).
/// Samples the hull loft at structural joint stations so manufactured segments follow the
/// changing cross-section — not a mid-bay axis-aligned bar.
/// </summary>
internal sealed class HullKnucklePath
{
    public const int KnuckleCount = 8;

    public HullKnucklePath(int vertexIndex, IReadOnlyList<HullKnuckleSample> samples)
    {
        if (vertexIndex is < 0 or >= KnuckleCount)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex));
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count < 2)
            throw new ArgumentException("Knuckle path needs at least two samples.", nameof(samples));

        VertexIndex = vertexIndex;
        Samples = samples;
        AssemblyId = $"knuckle-v{vertexIndex}";
    }

    public int VertexIndex { get; }
    public string AssemblyId { get; }
    public IReadOnlyList<HullKnuckleSample> Samples { get; }

    public float ZStart => Samples[0].ZStem;
    public float ZEnd => Samples[^1].ZStem;

    /// <summary>
    /// Build eight knuckle paths by sampling <see cref="HullGeometry.InnerOctagonRing"/> at each
    /// structural joint Z (frames + heavy bulkheads + explicit ends).
    /// </summary>
    public static IReadOnlyList<HullKnucklePath> Build(
        HullGeometry hull,
        float skinInset,
        IReadOnlyList<float> jointZsStem)
    {
        ArgumentNullException.ThrowIfNull(hull);
        ArgumentNullException.ThrowIfNull(jointZsStem);

        var zs = jointZsStem
            .Where(z => hull.HasStructuralSection(z, minHalf: 0.35f))
            .Distinct()
            .OrderBy(z => z)
            .ToList();
        if (zs.Count < 2)
            throw new InvalidOperationException("HullKnucklePath requires ≥2 structural joint stations.");

        var rings = new List<(float Y, float Up)[]>(zs.Count);
        foreach (var z in zs)
        {
            var ring = hull.InnerOctagonRing(z, skinInset);
            if (ring.Length != KnuckleCount)
                throw new InvalidOperationException(
                    $"Expected {KnuckleCount}-vertex knuckle ring at z={z:0.##}; got {ring.Length}.");
            rings.Add(ring);
        }

        var paths = new List<HullKnucklePath>(KnuckleCount);
        for (var v = 0; v < KnuckleCount; v++)
        {
            var samples = new List<HullKnuckleSample>(zs.Count);
            for (var i = 0; i < zs.Count; i++)
                samples.Add(new HullKnuckleSample(zs[i], rings[i][v].Y, rings[i][v].Up));
            paths.Add(new HullKnucklePath(v, samples));
        }

        return paths;
    }

    /// <summary>
    /// Merge ordinary-frame and heavy-bulkhead stations into one ordered joint list,
    /// always including stem/stern clamps when they have structure.
    /// </summary>
    public static IReadOnlyList<float> MergeJointStations(
        float loa,
        IEnumerable<float> frameZs,
        IEnumerable<float> bulkheadZs,
        float sternClear = 0.05f)
    {
        var set = new SortedSet<float>();
        foreach (var z in frameZs)
            set.Add(z);
        foreach (var z in bulkheadZs)
            set.Add(z);
        set.Add(0f);
        set.Add(MathF.Max(0.5f, loa - sternClear));
        return set.ToList();
    }
}
